#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Mqtt;
using DaleGhent.NINA.GroundStation.Utilities;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.FailuresToMqttTrigger {

    [ExportMetadata("Name", "Failures to MQTT broker")]
    [ExportMetadata("Description", "Publishes an informative JSON object to an MQTT broker when a sequence instruction fails")]
    [ExportMetadata("Icon", "Mqtt_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToMqttTrigger : SequenceTrigger, IValidatable, IDisposable {
        private readonly MqttCommon mqtt;
        private string topic;
        private int qos = 0;

        private ISequenceRootContainer failureHook;
        private readonly BackgroundQueueWorker<SequenceEntityFailureEventArgs> queueWorker;

        [ImportingConstructor]
        public FailuresToMqttTrigger() {
            queueWorker = new BackgroundQueueWorker<SequenceEntityFailureEventArgs>(1000, WorkerFn);
            mqtt = new MqttCommon();
            Topic = Properties.Settings.Default.MqttDefaultTopic;
            QoS = Properties.Settings.Default.MqttDefaultFailureQoSLevel;
        }

        public FailuresToMqttTrigger(FailuresToMqttTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Topic {
            get => topic;
            set {
                topic = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int QoS {
            get => qos;
            set {
                qos = value;
                RaisePropertyChanged();
            }
        }

        public override void Initialize() {
            _ = queueWorker.Start();
        }

        public override void Teardown() {
            queueWorker.Stop();
        }

        public void Dispose() {
            queueWorker.Dispose();
        }

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null && failureHook != null) {
                // When trigger is removed from sequence, unregister event handler
                // This could potentially be skipped by just using weak events instead
                failureHook.FailureEvent -= Root_FailureEvent;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                queueWorker.Stop();
                // When dragging the item into the sequence while the sequence is already running
                // Make sure to register the event handler as "SequenceBlockInitialized" is already done
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
                _ = queueWorker.Start();
            }
            base.AfterParentChanged();
        }

        public override void SequenceBlockInitialize() {
            // Register failure event when the parent context starts
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent += Root_FailureEvent;
            }
            base.SequenceBlockInitialize();
        }

        public override void SequenceBlockTeardown() {
            // Unregister failure event when the parent context ends
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent -= Root_FailureEvent;
            }
        }

        private async Task Root_FailureEvent(object arg1, SequenceEntityFailureEventArgs arg2) {
            if (arg2.Entity == null) {
                // An exception without context has occurred. Not sure when this can happen
                // Todo: Might be worthwile to send in a different style
                return;
            }

            if (arg2.Entity is FailuresToMqttTrigger || arg2.Entity is SendToMqtt.SendToMqtt) {
                // Prevent mqtt items to send mqtt failures
                return;
            }

            await queueWorker.Enqueue(arg2);
        }

        private async Task WorkerFn(SequenceEntityFailureEventArgs item, CancellationToken token) {
            var failedItem = FailedItem.FromEntity(item.Entity, item.Exception);
            var target = Utilities.Utilities.FindDsoInfo(item.Entity.Parent);
            var now = DateTime.Now;

            var itemInfo = new PreviousItem {
                version = 2,
                name = failedItem.Name,
                description = failedItem.Description,
                attempts = failedItem.Attempts,
                date_local = now.ToString("o"),
                date_utc = now.ToUniversalTime().ToString("o"),
                date_unix = Utilities.Utilities.UnixEpoch(),
                target_info = new List<TargetInfo>(),
                error_list = failedItem.Reasons,
            };

            if (target != null) {
                itemInfo.target_info.Add(new TargetInfo {
                    target_name = target.Name,
                    target_ra = target.Coordinates.RAString,
                    target_dec = target.Coordinates.DecString
                });
            }

            string payload = JsonConvert.SerializeObject(itemInfo);

            Logger.Info($"{this.Name}: {payload}");

            var attempts = 3; // Todo: Make it configurable?
            for (int i = 0; i < attempts; i++) {
                try {
                    var newCts = new CancellationTokenSource();
                    using (token.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                        await mqtt.PublishMessage(Topic, payload, QoS, newCts.Token);
                        break;
                    }
                } catch (Exception ex) {
                    Logger.Error($"Failed to send payload. Attempt {i + 1}/{attempts}", ex);
                }
            }
        }

        public IList<string> QoSLevels => MqttCommon.QoSLevels;

        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            return Task.CompletedTask;
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(mqtt.ValidateSettings());

            if (string.IsNullOrEmpty(Topic)) {
                i.Add("A topic is not defined");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToMqttTrigger(this) {
                Topic = Topic,
                QoS = QoS,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToMqttTrigger)}, Topic: {Topic}, QoS: {QoS}";
        }

        private class PreviousItem {
            public int version { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public string date_local { get; set; }
            public string date_utc { get; set; }
            public long date_unix { get; set; }
            public int attempts { get; set; }
            public List<TargetInfo> target_info { get; set; }
            public List<FailureReason> error_list { get; set; }
        }

        public class TargetInfo {
            public string target_name { get; set; }
            public string target_ra { get; set; }
            public string target_dec { get; set; }
        }
    }
}