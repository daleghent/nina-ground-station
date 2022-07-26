#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Utilities;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Mqtt {

    internal class MqttClient {
        private IMqttClient mqttClient;

        public MqttClient() {
            var mqttFactory = new MqttFactory();
            mqttClient = mqttFactory.CreateMqttClient();
        }

        public string ClientId { get; set; } = Properties.Settings.Default.MqttClientId;
        public string Topic { get; set; } = Properties.Settings.Default.MqttDefaultTopic;
        public string Payload { get; set; } = string.Empty;
        public string LastWillTopic { get; set; } = string.Empty;
        public string LastWillPayload { get; set; } = string.Empty;
        public int Qos { get; set; } = Properties.Settings.Default.MqttDefaultQoSLevel;
        public string MqttBrokerHost { get; } = Properties.Settings.Default.MqttBrokerHost;
        public ushort MqttBrokerPort { get; } = Properties.Settings.Default.MqttBrokerPort;
        public bool UseTls { get; } = Properties.Settings.Default.MqttBrokerUseTls;
        public int MaxReconnectAttempts { get; } = Properties.Settings.Default.MqttMaxReconnectAttempts;
        public string MqttUsername { get; } = Security.Decrypt(Properties.Settings.Default.MqttUsername);
        public string MqttPassword { get; } = Security.Decrypt(Properties.Settings.Default.MqttPassword);
        public bool IsConnected { get; set; } = false;
        public bool Shutdown { get; set; } = false;
        public MqttClientConnectResult MqttClientConnectResult { get; set; }
        public MqttApplicationMessage MqttApplicationMessage { get; set; }
        private int ReconnectAttempts { get; set; } = 0;

        public IMqttClientOptions Prepare() {
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(MqttBrokerHost, MqttBrokerPort)
                .WithCleanSession();

            if (!string.IsNullOrEmpty(ClientId) && !string.IsNullOrWhiteSpace(ClientId)) {
                var guid = Guid.NewGuid();
                var cidb = Encoding.UTF8.GetBytes(ClientId + "." + guid.ToString().Replace("-", ""));
                var clientIdBytes = new byte[23];
                Array.Copy(cidb, clientIdBytes, clientIdBytes.Length);

                ClientId = Encoding.UTF8.GetString(clientIdBytes);
                Logger.Info($"Using client ID {ClientId}");

                clientOptions.WithClientId(ClientId);
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

                mqttClient.UseConnectedHandler(e => {
                    Logger.Debug($"MQTT client has connected to {MqttBrokerHost}:{MqttBrokerPort}");
                });

                MqttClientConnectResult = await mqttClient.ConnectAsync(clientOptions, ct);
                IsConnected = mqttClient.IsConnected;

                mqttClient.UseDisconnectedHandler(async e => {
                    if (!Shutdown && !ct.IsCancellationRequested) {
                        if (ReconnectAttempts < MaxReconnectAttempts) {
                            ReconnectAttempts++;
                            Logger.Error($"MQTT client has been disconnected from {MqttBrokerHost}:{MqttBrokerPort}. Reconnecting({ReconnectAttempts})...");

                            if (ReconnectAttempts > 1) {
                                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                            }

                            var reconnectResult = await mqttClient.ReconnectAsync(ct);

                            if (reconnectResult.IsSessionPresent) {
                                ReconnectAttempts = 0;
                            }
                        } else {
                            Logger.Error($"MQTT broker reconnect attempts reached. Giving up.");
                            Notification.ShowError($"MQTT broker disconnected suddenly and could not be reconnected after {MaxReconnectAttempts} attempts. NINA client ID: {ClientId}");
                        }
                    }
                });

                return;
            } catch (Exception ex) {
                Logger.Error($"Error connecting to broker: {ex.Message}");
                throw;
            }
        }

        public async Task<MqttClientPublishResult> Publish(CancellationToken ct) {
            MqttClientPublishResult result = null;

            try {
                if (mqttClient.IsConnected) {
                    Logger.Debug("Sending payload to broker");

                    var payload = new MqttApplicationMessageBuilder()
                        .WithTopic(Topic)
                        .WithPayload(Payload)
                        .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)Qos)
                        .WithRetainFlag()
                        .Build();

                    result = await mqttClient.PublishAsync(payload, ct);
                }
            } catch (Exception ex) {
                Logger.Error($"Error publishing to broker: {ex.Message}");
                throw;
            }

            return result;
        }

        public async Task Ping(CancellationToken ct) {
            try {
                if (mqttClient.IsConnected) {
                    Logger.Debug("Sending ping to broker");

                    await mqttClient.PingAsync(ct);
                }
            } catch (Exception ex) {
                Logger.Error($"Error pinging broker: {ex.Message}");
                throw;
            }
        }

        public async Task Disconnect(CancellationToken ct) {
            try {
                Logger.Debug("Sending Disconnect to broker");
                Shutdown = true;

                if (mqttClient.IsConnected) {
                    await mqttClient.DisconnectAsync(ct);
                    IsConnected = mqttClient.IsConnected;
                    mqttClient.Dispose();
                }
            } catch (Exception ex) {
                Logger.Error($"Error disconnecting from broker: {ex.Message}");
                throw;
            }
        }

        public async Task Subscribe(CancellationToken ct) {
            try {
                if (mqttClient.IsConnected) {
                    Logger.Debug($"Subscribing to topic \"{Topic}\"");

                    mqttClient.UseApplicationMessageReceivedHandler(e => {
                        Logger.Info($"Received message: {e.ClientId}: Topic: {e.ApplicationMessage.Topic}, Qos: {e.ApplicationMessage.QualityOfServiceLevel}, Message: {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                        MqttApplicationMessage = e.ApplicationMessage;
                    });

                    var options = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(Topic)
                        .Build();

                    await mqttClient.SubscribeAsync(options, ct);
                }
            } catch (Exception ex) {
                Logger.Error($"Error subscribing to topic \"{Topic}\": {ex.Message}");
                throw;
            }
        }
    }
}