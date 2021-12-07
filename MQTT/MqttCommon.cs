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
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Mqtt {

    public class MqttCommon {

        public MqttCommon() {
            MqttBrokerHost = Properties.Settings.Default.MqttBrokerHost;
            MqttBrokerPort = Properties.Settings.Default.MqttBrokerPort;
            MqttBrokerUseTls = Properties.Settings.Default.MqttBrokerUseTls;
            MqttClientId = Properties.Settings.Default.MqttClientId;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task PublishMessage(string topic, string message, int qos, CancellationToken ct) {
            var mqttClient = new MqttClient() {
                Topic = topic,
                Payload = message,
                Qos = qos,
            };

            Logger.Debug($"Publishing message to {MqttBrokerHost}:{MqttBrokerPort}, UseTLS={MqttBrokerUseTls}, Topic={topic}");

            try {
                var options = mqttClient.Prepare();

                await mqttClient.Connect(options, ct);
                await mqttClient.Publish(ct);
                await mqttClient.Disconnect(ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to MQTT broker: {ex.Message}");
                throw ex;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(MqttBrokerHost) || string.IsNullOrWhiteSpace(MqttBrokerHost)) {
                issues.Add("MQTT broker hostname or IP not configured");
            }

            return issues;
        }

        public static readonly IList<string> QoSLevels = new List<string> {
            "0 - At Most Once",
            "1 - At Least Once",
            "2 - Exactly Once",
        };

        private string MqttBrokerHost { get; set; }
        private ushort MqttBrokerPort { get; set; }
        private string MqttClientId { get; set; }
        private bool MqttBrokerUseTls { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "MqttBrokerHost":
                    MqttBrokerHost = Properties.Settings.Default.MqttBrokerHost;
                    break;

                case "MqttBrokerPort":
                    MqttBrokerPort = Properties.Settings.Default.MqttBrokerPort;
                    break;

                case "MqttBrokerUseTls":
                    MqttBrokerUseTls = Properties.Settings.Default.MqttBrokerUseTls;
                    break;

                case "MqttClientId":
                    MqttClientId = Properties.Settings.Default.MqttClientId;
                    break;
            }
        }
    }
}