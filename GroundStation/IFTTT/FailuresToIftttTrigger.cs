#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

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
        private ISequenceItem previousItem;
        private string eventName = "nina";

        [ImportingConstructor]
        public FailuresToIftttTrigger() {
            WebhookKey = Properties.Settings.Default.IFTTTWebhookKey;

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

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var dict = new Dictionary<string, string> {
                { "value1", this.previousItem.Name },
                { "value2", this.previousItem.Attempts.ToString() },
                { "value3", this.previousItem.GetEstimatedDuration().TotalSeconds.ToString() }
            };

            Logger.Debug($"IftttTrigger: Pushing message: {dict.Values}");

            await IftttCommon.IftttCommon.SendIftttTrigger(JsonConvert.SerializeObject(dict), EventName, WebhookKey, ct);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (previousItem == null) {
                Logger.Debug("IftttTrigger: Previous item is null. Asserting false");
                return false;
            }

            this.previousItem = previousItem;

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("IFTTT")) {
                Logger.Debug($"IftttTrigger: Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                return true;
            }

            Logger.Debug($"IftttTrigger: Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            return false;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(WebhookKey)) {
                i.Add("IFTTT Webhooks key is missing");
            }

            if (string.IsNullOrEmpty(EventName)) {
                i.Add("IFTTT Webhooks event name is missing");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToIftttTrigger() {
                Icon = Icon,
                Name = Name,
                WebhookKey = WebhookKey,
                EventName = EventName,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToIftttTrigger)}";
        }

        private string WebhookKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "IFTTTWebhookKey":
                    WebhookKey = Properties.Settings.Default.IFTTTWebhookKey;
                    break;
            }
        }
    }
}