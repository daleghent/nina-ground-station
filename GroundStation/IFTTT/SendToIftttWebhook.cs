#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.SendToIftttWebhook {

    [ExportMetadata("Name", "Send to IFTTT Webhooks")]
    [ExportMetadata("Description", "Sends a free form 3-value message to IFTTT Webhooks")]
    [ExportMetadata("Icon", "IFTTT_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToIftttWebhook : SequenceItem, IValidatable {
        private string eventName = "nina";
        private string value1 = string.Empty;
        private string value2 = string.Empty;
        private string value3 = string.Empty;

        [ImportingConstructor]
        public SendToIftttWebhook() {
            IFTTTWebhookKey = Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);
            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public SendToIftttWebhook(SendToIftttWebhook copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string EventName {
            get => eventName;
            set {
                if (!value.Contains("/")) {
                    eventName = value;
                }

                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value1 {
            get => value1;
            set {
                value1 = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value2 {
            get => value2;
            set {
                value2 = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value3 {
            get => value3;
            set {
                value3 = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var dict = new Dictionary<string, string> {
                { "value1", value1 },
                { "value2", value2 },
                { "value3", value3 }
            };

            await IftttCommon.IftttCommon.SendIftttTrigger(JsonConvert.SerializeObject(dict), EventName, IFTTTWebhookKey, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(IFTTTWebhookKey) || string.IsNullOrWhiteSpace(IFTTTWebhookKey)) {
                i.Add("IFTTT Webhook key is missing");
            }

            if (string.IsNullOrEmpty(EventName) || string.IsNullOrWhiteSpace(EventName)) {
                i.Add("IFTTT Webhook event name is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToIftttWebhook() {
                Icon = Icon,
                Name = Name,
                IFTTTWebhookKey = IFTTTWebhookKey,
                EventName = EventName,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToIftttWebhook)}";
        }

        private string IFTTTWebhookKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "IFTTTWebhookKey":
                    IFTTTWebhookKey = Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);
                    break;
            }
        }
    }
}