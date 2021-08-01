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
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.SendToMqtt {

    [ExportMetadata("Name", "Send to MQTT")]
    [ExportMetadata("Description", "Sends a free form message to a MQTT broker")]
    [ExportMetadata("Icon", "Mqtt_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToMqtt : SequenceItem, IValidatable {
        private MqttCommon mqtt;
        private string topic;
        private string payload = string.Empty;

        [ImportingConstructor]
        public SendToMqtt() {
            mqtt = new MqttCommon();
            Topic = Properties.Settings.Default.MqttDefaultTopic;
        }

        public SendToMqtt(SendToMqtt copyMe) : this() {
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
        public string Payload {
            get => payload;
            set {
                payload = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            Logger.Trace($"{this}: {Payload}");
            await mqtt.PublishMessge(Topic, Payload, ct);
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
            return new SendToMqtt() {
                Icon = Icon,
                Name = Name,
                Topic = Topic,
                Payload = Payload,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToMqtt)}";
        }
    }
}