#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Ifttt;
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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.FailuresToIftttTrigger {

    [ExportMetadata("Name", "Failures to IFTTT")]
    [ExportMetadata("Description", "Sends an event to IFTTT Webhooks when a sequence instruction fails")]
    [ExportMetadata("Icon", "IFTTT_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToIftttTrigger : SequenceTrigger, IValidatable, INotifyPropertyChanged {
        private IftttCommon ifttt;
        private string eventName = "nina";

        private ISequenceRootContainer failureHook;
        private BackgroundQueueWorker<SequenceEntityFailureEventArgs> queueWorker;

        [ImportingConstructor]
        public FailuresToIftttTrigger() {
            queueWorker = new BackgroundQueueWorker<SequenceEntityFailureEventArgs>(1000, WorkerFn);
            ifttt = new IftttCommon();

            IftttFailureValue1 = Properties.Settings.Default.IftttFailureValue1;
            IftttFailureValue2 = Properties.Settings.Default.IftttFailureValue2;
            IftttFailureValue3 = Properties.Settings.Default.IftttFailureValue3;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToIftttTrigger(FailuresToIftttTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string EventName {
            get => eventName;
            set {
                if (value.Contains("/") || value.Contains(" ")) {
                    RaisePropertyChanged();
                    return;
                }

                eventName = value;
                RaisePropertyChanged();
            }
        }

        public override void Initialize() {
            queueWorker.Stop();
            _ = queueWorker.Start();
        }

        public override void Teardown() {
            queueWorker.Stop();
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

            if (arg2.Entity is FailuresToIftttTrigger || arg2.Entity is SendToIftttWebhook.SendToIftttWebhook) {
                // Prevent ifttt items to send ifttt failures
                return;
            }

            await queueWorker.Enqueue(arg2);
        }

        private async Task WorkerFn(SequenceEntityFailureEventArgs item, CancellationToken token) {
            var failedItem = FailedItem.FromEntity(item.Entity, item.Exception);

            var dict = new Dictionary<string, string>();

            dict.Add("value1", ResolveAllTokens(IftttFailureValue1, failedItem));
            dict.Add("value2", ResolveAllTokens(IftttFailureValue2, failedItem));
            dict.Add("value3", ResolveAllTokens(IftttFailureValue3, failedItem));

            Logger.Debug($"Pushing message: {string.Join(" || ", dict.Values)}");

            var attempts = 3; // Todo: Make it configurable?
            for (int i = 0; i < attempts; i++) {
                try {
                    var newCts = new CancellationTokenSource();
                    using (token.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                        await ifttt.SendIftttWebhook(JsonConvert.SerializeObject(dict), EventName, newCts.Token);
                    }
                } catch (Exception ex) {
                    Logger.Error($"Email failed to send message. Attempt {i + 1}/{attempts}", ex);
                }
            }
        }

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
            var i = new List<string>(ifttt.ValidateSettings());

            if (string.IsNullOrEmpty(EventName) || string.IsNullOrWhiteSpace(EventName)) {
                i.Add("IFTTT Webhooks event name is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToIftttTrigger(this) {
                EventName = EventName,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToIftttTrigger)}";
        }

        private string IftttFailureValue1 { get; set; }
        private string IftttFailureValue2 { get; set; }
        private string IftttFailureValue3 { get; set; }

        private string ResolveAllTokens(string text, FailedItem failedItem) {
            text = Utilities.Utilities.ResolveTokens(text, this.Parent);
            text = Utilities.Utilities.ResolveFailureTokens(text, failedItem);

            return text;
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "IftttFailureValue1":
                    IftttFailureValue1 = Properties.Settings.Default.IftttFailureValue1;
                    break;

                case "IftttFailureValue2":
                    IftttFailureValue2 = Properties.Settings.Default.IftttFailureValue2;
                    break;

                case "IftttFailureValue3":
                    IftttFailureValue3 = Properties.Settings.Default.IftttFailureValue3;
                    break;
            }
        }
    }
}