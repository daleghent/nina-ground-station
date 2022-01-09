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
        private ISequenceItem previousItem;
        private string eventName = "nina";

        [ImportingConstructor]
        public FailuresToIftttTrigger() {
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

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            foreach (var failedItem in FailedItems) {
                var dict = new Dictionary<string, string>();

                dict.Add("value1", ResolveAllTokens(IftttFailureValue1, failedItem));
                dict.Add("value2", ResolveAllTokens(IftttFailureValue2, failedItem));
                dict.Add("value3", ResolveAllTokens(IftttFailureValue3, failedItem));

                Logger.Debug($"Pushing message: {string.Join(" || ", dict.Values)}");

                await ifttt.SendIftttWebhook(JsonConvert.SerializeObject(dict), EventName, ct);
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

            if (this.previousItem.Name.Contains("IFTTT") && this.previousItem.Category.Equals(Category)) {
                Logger.Debug("Previous item is related. Asserting false");
                return false;
            }

            FailedItems.Clear();
            FailedItems = Utilities.Utilities.GetFailedItems(this.previousItem);

            return FailedItems.Count > 0;
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

        private List<FailedItem> FailedItems { get; set; } = new List<FailedItem>();

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