#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Protocol;
using NINA.Core.Utility;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Mqtt {

    internal class MqttClient {
        private IMqttClient mqttClient;

        public MqttClient() {
            MqttFactory mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
        }

        public string ClientId { get; set; } = Properties.Settings.Default.MqttClientId;
        public string Topic { get; set; } = Properties.Settings.Default.MqttDefaultTopic;
        public string Payload { get; set; } = string.Empty;
        public string LastWillTopic { get; set; } = string.Empty;
        public string LastWillPayload { get; set; } = string.Empty;
        public int Qos { get; set; } = Properties.Settings.Default.MqttDefaultQoSLevel;
        public string MqttBrokerHost { get; set; } = Properties.Settings.Default.MqttBrokerHost;
        public ushort MqttBrokerPort { get; set; } = Properties.Settings.Default.MqttBrokerPort;
        public bool UseTls { get; set; } = Properties.Settings.Default.MqttBrokerUseTls;
        public string MqttUsername { get; set; } = Security.Decrypt(Properties.Settings.Default.MqttUsername);
        public string MqttPassword { get; set; } = Security.Decrypt(Properties.Settings.Default.MqttPassword);
        public bool IsConnected { get; set; } = false;
        public bool Shutdown { get; set; } = false;
        public MqttClientConnectResult MqttClientConnectResult { get; set; }

        public IMqttClientOptions Prepare() {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(MqttBrokerHost, MqttBrokerPort)
                .WithCleanSession();

            if (!string.IsNullOrEmpty(ClientId) && !string.IsNullOrWhiteSpace(ClientId)) {
                var guid = Guid.NewGuid();
                var cidb = Encoding.UTF8.GetBytes(ClientId + "." + guid.ToString().Replace("-", ""));
                var clientIdBytes = new byte[23];
                Array.Copy(cidb, clientIdBytes, clientIdBytes.Length);

                var cids = Encoding.UTF8.GetString(clientIdBytes);
                Logger.Info($"Using client ID {cids}");

                clientOptions.WithClientId(cids);
            }

            if (!string.IsNullOrEmpty(LastWillTopic) && !string.IsNullOrWhiteSpace(LastWillTopic) && !string.IsNullOrEmpty(LastWillPayload) && !string.IsNullOrWhiteSpace(LastWillPayload)) {
                var lwtPayload = new MqttApplicationMessageBuilder()
                    .WithTopic(LastWillTopic)
                    .WithPayload(LastWillPayload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)Qos)
                    .WithRetainFlag()
                    .Build();

                clientOptions.WithWillMessage(lwtPayload);
                clientOptions.WithKeepAlivePeriod(TimeSpan.FromSeconds(2));
            }

            if (!string.IsNullOrEmpty(MqttUsername) && !string.IsNullOrWhiteSpace(MqttUsername)) {
                clientOptions.WithCredentials(MqttUsername, MqttPassword);
            }

            if (UseTls) {
                clientOptions.WithTls();
            }

            return clientOptions.Build();
        }

        public async Task Connect(IMqttClientOptions clientOptions, CancellationToken ct) {
            try {
                Logger.Debug("Connecting to broker");

                mqttClient.UseDisconnectedHandler(e => {
                    if (!Shutdown) {
                        Logger.Error($"MQTT client has been disconnected from {MqttBrokerHost}:{MqttBrokerPort}. Reconnecting...");
                        mqttClient.ReconnectAsync(ct);
                    }
                });

                mqttClient.UseConnectedHandler(e => {
                    Logger.Info($"MQTT client has connected to {MqttBrokerHost}:{MqttBrokerPort}");
                });

                MqttClientConnectResult = await mqttClient.ConnectAsync(clientOptions, ct);
                IsConnected = true;

                return;
            } catch (Exception ex) {
                Logger.Error($"Error connecting to MQTT broker: {ex.Message}");
                throw ex;
            }
        }

        public async Task<MqttClientPublishResult> Publish(CancellationToken ct) {
            try {
                Logger.Debug("Sending payload to broker");

                var payload = new MqttApplicationMessageBuilder()
                    .WithTopic(Topic)
                    .WithPayload(Payload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)Qos)
                    .WithRetainFlag()
                    .Build();

                var result = await mqttClient.PublishAsync(payload, ct);
                return result;
            } catch (Exception ex) {
                Logger.Error($"Error publishing to MQTT broker: {ex.Message}");
                throw ex;
            }
        }

        public async Task Ping(CancellationToken ct) {
            try {
                Logger.Debug("Sending ping to broker");

                await mqttClient.PingAsync(ct);
            } catch (Exception ex) {
                Logger.Error($"Error disconnecting from MQTT broker: {ex.Message}");
                throw ex;
            }
        }

        public async Task Disconnect(CancellationToken ct) {
            try {
                Logger.Debug("Sending Disconnect to broker");
                Shutdown = true;

                await mqttClient.DisconnectAsync(ct);
                mqttClient.Dispose();
                IsConnected = false;
            } catch (Exception ex) {
                Logger.Error($"Error disconnecting from MQTT broker: {ex.Message}");
                throw ex;
            }
        }
    }
}