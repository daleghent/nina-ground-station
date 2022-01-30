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
    public class FailuresToMqttTrigger : SequenceTrigger, IValidatable {
        private MqttCommon mqtt;
        private ISequenceItem previousItem;
        private string topic;
        private int qos = 0;

        [ImportingConstructor]
        public FailuresToMqttTrigger() {
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

        public IList<string> QoSLevels => MqttCommon.QoSLevels;

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var target = Utilities.Utilities.FindDsoInfo(previousItem.Parent);
            var now = DateTime.Now;

            foreach (var failedItem in FailedItems) {
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

                Logger.Debug($"{this}: {payload}");

                var newCts = new CancellationTokenSource();
                using (ct.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                    await mqtt.PublishMessage(Topic, payload, QoS, newCts.Token);
                }
            }

            FailedItems.Clear();
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (previousItem == null) {
                Logger.Debug("Previous item is null. Asserting false");
                return false;
            }

            this.previousItem = previousItem;

            this.previousItem.Name = this.previousItem.Name ?? this.previousItem.ToString();
            this.previousItem.Category = this.previousItem.Category ?? this.previousItem.ToString();

            if (this.previousItem.Name.Contains("MQTT") && this.previousItem.Category.Equals(Category)) {
                Logger.Debug("Previous item is related. Asserting false");
                return false;
            }

            FailedItems.Clear();
            FailedItems = Utilities.Utilities.GetFailedItems(this.previousItem);

            return FailedItems.Count > 0;
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

        private List<Utilities.FailedItem> FailedItems { get; set; } = new List<Utilities.FailedItem>();

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