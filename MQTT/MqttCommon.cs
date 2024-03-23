#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Mqtt {

    public class MqttCommon {

        public MqttCommon() {
        }

        public static async Task PublishMessage(string topic, string message, int qos, CancellationToken ct, string contentType = "text/plain") {
            var mqttClient = new MqttClient() {
                Topic = topic,
                Payload = message,
                Qos = qos,
                ContentType = contentType,
            };

            Logger.Debug($"Publishing message to {GroundStation.GroundStationConfig.MqttBrokerHost}:{GroundStation.GroundStationConfig.MqttBrokerPort}, UseTLS={GroundStation.GroundStationConfig.MqttBrokerUseTls}, Topic={topic}");

            try {
                var options = mqttClient.Prepare();

                await mqttClient.Connect(options, ct);
                await mqttClient.Publish(ct);
                await mqttClient.Disconnect(ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to MQTT broker: {ex.Message}");
                throw;
            }
        }

        public static async Task PublishByteMessage(string topic, byte[] payload, int qos, string contentType, CancellationToken ct) {
            var mqttClient = new MqttClient() {
                Topic = topic,
                BytePayload = payload,
                Qos = qos,
                ContentType = contentType,
            };

            Logger.Debug($"Publishing message to {GroundStation.GroundStationConfig.MqttBrokerHost}:{GroundStation.GroundStationConfig.MqttBrokerPort}, UseTLS={GroundStation.GroundStationConfig.MqttBrokerUseTls}, Topic={topic}");

            try {
                var options = mqttClient.Prepare();

                await mqttClient.Connect(options, ct);
                await mqttClient.PublishBytes(ct);
                await mqttClient.Disconnect(ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to MQTT broker: {ex.Message}");
                throw;
            }
        }

        public static IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.MqttBrokerHost)) {
                issues.Add("MQTT broker hostname or IP not configured");
            }

            return issues;
        }

        public static readonly IList<string> QoSLevels = new List<string> {
            "0 - At Most Once",
            "1 - At Least Once",
            "2 - Exactly Once",
        };
    }
}