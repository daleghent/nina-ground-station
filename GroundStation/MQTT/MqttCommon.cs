#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using MQTTnet;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
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
            MqttUsername = Security.Decrypt(Properties.Settings.Default.MqttUsername);
            MqttPassword = Security.Decrypt(Properties.Settings.Default.MqttPassword);
            MqttClientId = Properties.Settings.Default.MqttClientId;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task PublishMessge(string topic, string message, CancellationToken ct) {
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithClientId(MqttClientId)
                .WithTcpServer(MqttBrokerHost, MqttBrokerPort)
                .WithCleanSession();

            if (!string.IsNullOrEmpty(MqttUsername) && !string.IsNullOrWhiteSpace(MqttUsername)) {
                options.WithCredentials(MqttUsername, MqttPassword);
            }

            if (MqttBrokerUseTls) {
                options.WithTls();
            }

            var opts = options.Build();

            var payload = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithExactlyOnceQoS()
                .WithRetainFlag()
                .Build();

            var discopts = new MqttClientDisconnectOptions();

            Logger.Debug($"Publishing message to {MqttBrokerHost}:{MqttBrokerPort}, UseTLS={MqttBrokerUseTls}, Topic={topic}");

            try {
                await mqttClient.ConnectAsync(opts, ct);
                await mqttClient.PublishAsync(payload, ct);
                await mqttClient.DisconnectAsync(discopts, ct);
                mqttClient.Dispose();
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

            if (string.IsNullOrEmpty(MqttClientId) || string.IsNullOrWhiteSpace(MqttClientId)) {
                issues.Add("MQTT client ID is invalid!");
            }

            return issues;
        }

        private string MqttBrokerHost { get; set; }
        private ushort MqttBrokerPort { get; set; }
        private string MqttUsername { get; set; }
        private string MqttPassword { get; set; }
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
                case "MqttUsername":
                    MqttUsername = Security.Decrypt(Properties.Settings.Default.MqttUsername);
                    break;
                case "MqttPassword":
                    MqttPassword = Security.Decrypt(Properties.Settings.Default.MqttPassword);
                    break;
                case "MqttClientId":
                    MqttClientId = Properties.Settings.Default.MqttClientId;
                    break;
            }
        }
    }
}
