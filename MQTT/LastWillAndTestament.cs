#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Config;
using MQTTnet.Client.Publishing;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Mqtt {

    public class LastWillAndTestament {
        private readonly MqttClient lwtClient;
        private readonly GroundStationConfig groundStationConfig;

        public LastWillAndTestament(GroundStationConfig groundStationOptions) {
            this.groundStationConfig = groundStationOptions;

            lwtClient = new MqttClient() {
                Payload = Utilities.Utilities.ResolveTokens(groundStationOptions.MqttLwtBirthPayload),
                LastWillTopic = groundStationOptions.MqttLwtTopic,
                LastWillPayload = Utilities.Utilities.ResolveTokens(groundStationOptions.MqttLwtLastWillPayload),
                Qos = groundStationOptions.MqttDefaultFailureQoSLevel,
            };
        }

        public async Task StartLwtSession(CancellationToken ct) {
            if (!groundStationConfig.MqttLwtEnabled || lwtClient.IsConnected) {
                return;
            } else if (!lwtClient.IsConnected) {
                var clientOpts = lwtClient.Prepare();

                await lwtClient.Connect(clientOpts, ct);
                var result = await lwtClient.Publish(ct);

                if (result.ReasonCode != MqttClientPublishReasonCode.Success) {
                    var errorMesg = $"Failed to publish LWT message to topic {groundStationConfig.MqttLwtTopic}: {result.ReasonString}";
                    Logger.Error(errorMesg);
                    Notification.ShowError(errorMesg);
                }

                Logger.Info($"Started MQTT LWT service. Sending to topic {groundStationConfig.MqttLwtTopic}");
            }
        }

        public async Task StopLwtSession(CancellationToken ct) {
            if (!groundStationConfig.MqttLwtEnabled && !lwtClient.IsConnected) {
                return;
            } else if (lwtClient.IsConnected) {
                lwtClient.Payload = Utilities.Utilities.ResolveTokens(groundStationConfig.MqttLwtClosePayload);

                await lwtClient.Publish(ct);
                await lwtClient.Disconnect(ct);

                Logger.Info($"Stopped MQTT LWT service");
            }
        }

        public bool IsConnected => lwtClient.IsConnected;
    }
}