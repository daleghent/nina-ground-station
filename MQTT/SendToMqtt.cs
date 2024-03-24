#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using DaleGhent.NINA.GroundStation.Mqtt;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.SendToMqtt {

    [ExportMetadata("Name", "Publish to MQTT broker")]
    [ExportMetadata("Description", "Publishes a free form message to a MQTT broker")]
    [ExportMetadata("Icon", "Mqtt_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToMqtt : SequenceItem, IValidatable {
        private readonly MqttCommon mqtt;
        private string topic;
        private string payload = string.Empty;
        private int qos = 0;

        private readonly ICameraMediator cameraMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly ISwitchMediator switchMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IWeatherDataMediator weatherDataMediator;

        private readonly IMetadata metadata;

        [ImportingConstructor]
        public SendToMqtt(ICameraMediator cameraMediator,
                             IDomeMediator domeMediator,
                             IFilterWheelMediator filterWheelMediator,
                             IFlatDeviceMediator flatDeviceMediator,
                             IFocuserMediator focuserMediator,
                             IGuiderMediator guiderMediator,
                             IRotatorMediator rotatorMediator,
                             ISafetyMonitorMediator safetyMonitorMediator,
                             ISwitchMediator switchMediator,
                             ITelescopeMediator telescopeMediator,
                             IWeatherDataMediator weatherDataMediator) {

            this.cameraMediator = cameraMediator;
            this.domeMediator = domeMediator;
            this.guiderMediator = guiderMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.rotatorMediator = rotatorMediator;
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.switchMediator = switchMediator;
            this.telescopeMediator = telescopeMediator;
            this.weatherDataMediator = weatherDataMediator;

            metadata = new Metadata(cameraMediator,
                domeMediator, filterWheelMediator, flatDeviceMediator, focuserMediator,
                guiderMediator, rotatorMediator, safetyMonitorMediator, switchMediator,
                telescopeMediator, weatherDataMediator);

            mqtt = new MqttCommon();
            Topic = GroundStation.GroundStationConfig.MqttDefaultTopic;
            QoS = GroundStation.GroundStationConfig.MqttDefaultQoSLevel;
        }

        public SendToMqtt() {
            mqtt = new MqttCommon();
            Topic = GroundStation.GroundStationConfig.MqttDefaultTopic;
            QoS = GroundStation.GroundStationConfig.MqttDefaultQoSLevel;
        }

        public SendToMqtt(SendToMqtt copyMe) : this(cameraMediator: copyMe.cameraMediator,
                                                    domeMediator: copyMe.domeMediator,
                                                    filterWheelMediator: copyMe.filterWheelMediator,
                                                    flatDeviceMediator: copyMe.flatDeviceMediator,
                                                    focuserMediator: copyMe.focuserMediator,
                                                    guiderMediator: copyMe.guiderMediator,
                                                    rotatorMediator: copyMe.rotatorMediator,
                                                    safetyMonitorMediator: copyMe.safetyMonitorMediator,
                                                    switchMediator: copyMe.switchMediator,
                                                    telescopeMediator: copyMe.telescopeMediator,
                                                    weatherDataMediator: copyMe.weatherDataMediator) {
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

        [JsonProperty]
        public int QoS {
            get => qos;
            set {
                qos = value;
                RaisePropertyChanged();
            }
        }

        public static IList<string> QoSLevels => MqttCommon.QoSLevels;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var payload = Utilities.Utilities.ResolveTokens(Payload, this, metadata);

            Logger.Trace($"{this}: {payload}");
            await MqttCommon.PublishMessage(Topic, payload, QoS, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(MqttCommon.ValidateSettings());

            if (string.IsNullOrEmpty(Topic)) {
                i.Add("A topic is not defined");
            }

            if (string.IsNullOrEmpty(Payload)) {
                i.Add("A payload is not defined");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToMqtt(this) {
                Topic = Topic,
                QoS = QoS,
                Payload = Payload,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Topic: {Topic}, QoS: {QoS}, PayloadLength={Payload.Length}";
        }
    }
}