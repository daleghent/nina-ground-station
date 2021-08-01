#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Mqtt;
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

        [ImportingConstructor]
        public FailuresToMqttTrigger() {
            mqtt = new MqttCommon();
            Topic = Properties.Settings.Default.MqttDefaultTopic;
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

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var itemInfo = new PreviousItem {
                name = previousItem.Name,
                description = previousItem.Description,
                attempts = previousItem.Attempts,
                error_list = new List<ErrorItems>()
            };

            foreach (var e in PreviousItemIssues) {
                itemInfo.error_list.Add(new ErrorItems { reason = e, });
            }

            string payload = JsonConvert.SerializeObject(itemInfo);

            Logger.Trace($"{this}: {payload}");

            await mqtt.PublishMessge(Topic, payload, ct);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            bool shouldTrigger = false;

            if (previousItem == null) {
                Logger.Debug("MqttTrigger: Previous item is null. Asserting false");
                return shouldTrigger; ;
            }

            if (previousItem == this.previousItem) {
                Logger.Debug("Previous item has already been processed. Asserting false");
                return shouldTrigger;
            }

            this.previousItem = previousItem;

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("MQTT")) {
                Logger.Debug($"MqttTrigger: Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                shouldTrigger = true;

                if (this.previousItem is IValidatable validatableItem && validatableItem.Issues.Count > 0) {
                    PreviousItemIssues = validatableItem.Issues;
                    Logger.Debug($"MqttTrigger: Previous item \"{this.previousItem.Name}\" had {PreviousItemIssues.Count} issues: {string.Join(", ", PreviousItemIssues)}");
                }
            } else {
                Logger.Debug($"MqttTrigger: Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            }

            return shouldTrigger;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(mqtt.ValidateSettings());

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToMqttTrigger() {
                Icon = Icon,
                Name = Name,
                Topic = Topic,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToMqttTrigger)}";
        }

        private IList<string> PreviousItemIssues { get; set; } = new List<string>();

        private class PreviousItem {
            public string name { get; set; }
            public string description { get; set; }
            public int attempts { get; set; }
            public List<ErrorItems> error_list { get; set; }
        }

        public class ErrorItems {
            public string reason { get; set; }
        }
    }
}