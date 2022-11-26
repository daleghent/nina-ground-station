#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Mqtt;
using DaleGhent.NINA.GroundStation.TTS;
using DaleGhent.NINA.GroundStation.Utilities;
using DaleGhent.NINA.GroundStation.PushoverClient;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation {

    [Export(typeof(IPluginManifest))]
    public class GroundStation : PluginBase, ISettings, INotifyPropertyChanged {
        private MqttClient mqttClient;

        [ImportingConstructor]
        public GroundStation() {
            if (Properties.Settings.Default.UpgradeSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            PushoverTestCommand = new AsyncCommand<bool>(PushoverTest);
            EmailTestCommand = new AsyncCommand<bool>(EmailTest);
            TelegramTestCommand = new AsyncCommand<bool>(TelegramTest);
            MQTTTestCommand = new AsyncCommand<bool>(MQTTTest);
            IFTTTTestCommand = new AsyncCommand<bool>(IFTTTTest);
            TtsTestCommand = new AsyncCommand<bool>(TtsTest);
        }

        public override Task Initialize() {
            if (MqttLwtEnable) {
                LwtStartWorker();
            }

            Logger.Debug("Init completed");

            return Task.CompletedTask;
        }

        public override async Task Teardown() {
            if (MqttLwtEnable) {
                await LwtStopWorker();
            }

            return;
        }

        private async Task<bool> PushoverTest(object arg) {
            var send = new SendToPushover.SendToPushover() {
                Message = "Test Message",
                Title = "Test Title",
                Attempts = 1
            };

            await send.Run(default, default);

            if (send.Status == SequenceEntityStatus.FAILED) {
                Notification.ShowExternalError($"Failed to send message to Pushover:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "Pushover Error");
                return false;
            } else {
                Notification.ShowSuccess("Pushover message sent");
                return true;
            }
        }

        private async Task<bool> EmailTest(object arg) {
            var send = new SendToEmail.SendToEmail() {
                Subject = "Test Subject",
                Body = "Test Body",
                Attempts = 1
            };

            if (send.Validate()) {
                await send.Run(default, default);
            } else {
                Notification.ShowExternalError($"Failed to send email:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "Email Error");
                return false;
            }

            if (send.Status == SequenceEntityStatus.FINISHED) {
                Notification.ShowSuccess("Email sent");
                return true;
            } else {
                // Something bad happened further down and should have produced an error notification. (runtime exception)
                return false;
            }
        }

        private async Task<bool> TelegramTest(object arg) {
            var send = new SendToTelegram.SendToTelegram() {
                Message = "Test Message",
                Attempts = 1
            };

            await send.Run(default, default);

            if (send.Status == SequenceEntityStatus.FAILED) {
                Notification.ShowExternalError($"Failed to send message to Telegram:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "Telegram Error");
                return false;
            } else {
                Notification.ShowSuccess("Telegram message sent");
                return true;
            }
        }

        private async Task<bool> MQTTTest(object arg) {
            var send = new SendToMqtt.SendToMqtt() {
                Topic = "Test Topic",
                Payload = "Test Payload",
                Attempts = 1
            };

            await send.Run(default, default);

            if (send.Status == SequenceEntityStatus.FAILED) {
                Notification.ShowExternalError($"Failed to send message to MQTT:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "MQTT Error");
                return false;
            } else {
                Notification.ShowSuccess("MQTT message sent");
                return true;
            }
        }

        private async Task<bool> IFTTTTest(object arg) {
            var send = new SendToIftttWebhook.SendToIftttWebhook() {
                Value1 = "Test Value1",
                Value2 = "Test Value2",
                Value3 = "Test Value3",
                Attempts = 1
            };

            await send.Run(default, default);

            if (send.Status == SequenceEntityStatus.FAILED) {
                Notification.ShowExternalError($"Failed to send message to IFTTT:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "IFTTT Error");
                return false;
            } else {
                Notification.ShowSuccess("IFTTT message sent");
                return true;
            }
        }

        private async Task<bool> TtsTest(object arg) {
            var send = new SendToTTS() {
                Message = TtsTestMessage,
                Attempts = 1
            };

            await send.Run(default, default);

            if (send.Status == SequenceEntityStatus.FAILED) {
                Notification.ShowExternalError($"Failed to send message to TTS:{Environment.NewLine}{string.Join(Environment.NewLine, send.Issues)}", "TTS Error");
                return false;
            }

            return true;
        }

        private Task LwtStartWorker() {
            return Task.Run(async () => {
                Logger.Info($"Starting MQTT LWT service. Sending to topic {MqttLwtTopic}");

                mqttClient = new MqttClient() {
                    Payload = Utilities.Utilities.ResolveTokens(MqttLwtBirthPayload),
                    LastWillTopic = MqttLwtTopic,
                    LastWillPayload = Utilities.Utilities.ResolveTokens(MqttLwtLastWillPayload),
                    Qos = MqttDefaultFailureQoSLevel,
                };

                var clientOpts = mqttClient.Prepare();

                await mqttClient.Connect(clientOpts, CancellationToken.None);
                await mqttClient.Publish(CancellationToken.None);

                Logger.Debug("Exiting LwtStartWorker task");
            });
        }

        private async Task LwtStopWorker() {
            Logger.Debug("Stopping LWT worker");

            mqttClient.Payload = Utilities.Utilities.ResolveTokens(MqttLwtClosePayload);
            await mqttClient.Publish(CancellationToken.None);

            await mqttClient.Disconnect(CancellationToken.None);

            return;
        }

        public IAsyncCommand PushoverTestCommand { get; }
        public IAsyncCommand EmailTestCommand { get; }
        public IAsyncCommand IFTTTTestCommand { get; }
        public IAsyncCommand TelegramTestCommand { get; }
        public IAsyncCommand MQTTTestCommand { get; }
        public IAsyncCommand TtsTestCommand { get; }

        public string IFTTTWebhookKey {
            get => Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);
            set {
                Properties.Settings.Default.IFTTTWebhookKey = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue1 {
            get => Properties.Settings.Default.IftttFailureValue1;
            set {
                Properties.Settings.Default.IftttFailureValue1 = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue2 {
            get => Properties.Settings.Default.IftttFailureValue2;
            set {
                Properties.Settings.Default.IftttFailureValue2 = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue3 {
            get => Properties.Settings.Default.IftttFailureValue3;
            set {
                Properties.Settings.Default.IftttFailureValue3 = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string PushoverAppKey {
            get => Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            set {
                Properties.Settings.Default.PushoverAppKey = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string PushoverUserKey {
            get => Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
            set {
                Properties.Settings.Default.PushoverUserKey = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public Priority[] PushoverPriorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().ToArray();

        public NotificationSound[] PushoverNotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

        public NotificationSound PushoverDefaultNotificationSound {
            get => (NotificationSound)Enum.Parse(typeof(NotificationSound), Properties.Settings.Default.PushoverDefaultNotificationSound);
            set {
                Properties.Settings.Default.PushoverDefaultNotificationSound = value.ToString();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultNotificationPriority {
            get => (Priority)Enum.Parse(typeof(Priority), Properties.Settings.Default.PushoverDefaultNotificationPriority);
            set {
                Properties.Settings.Default.PushoverDefaultNotificationPriority = value.ToString();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public NotificationSound PushoverDefaultFailureSound {
            get => (NotificationSound)Enum.Parse(typeof(NotificationSound), Properties.Settings.Default.PushoverDefaultFailureSound);
            set {
                Properties.Settings.Default.PushoverDefaultFailureSound = value.ToString();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultFailurePriority {
            get => (Priority)Enum.Parse(typeof(Priority), Properties.Settings.Default.PushoverDefaultFailurePriority);
            set {
                Properties.Settings.Default.PushoverDefaultFailurePriority = value.ToString();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int PushoverEmergRetryInterval {
            get => Properties.Settings.Default.PushoverEmergRetryInterval;
            set {
                if (value >= 30 && value <= 86400) {
                    Properties.Settings.Default.PushoverEmergRetryInterval = value;
                    CoreUtil.SaveSettings(Properties.Settings.Default);
                    RaisePropertyChanged();
                } else {
                    RaisePropertyChanged();
                }
            }
        }

        public int PushoverEmergExpireAfter {
            get => Properties.Settings.Default.PushoverEmergExpireAfter;
            set {
                if (value >= 30 && value <= 86400) {
                    Properties.Settings.Default.PushoverEmergExpireAfter = value;
                    CoreUtil.SaveSettings(Properties.Settings.Default);
                    RaisePropertyChanged();
                } else {
                    RaisePropertyChanged();
                }
            }
        }

        public string PushoverFailureTitleText {
            get => Properties.Settings.Default.PushoverFailureTitleText;
            set {
                Properties.Settings.Default.PushoverFailureTitleText = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string PushoverFailureBodyText {
            get => Properties.Settings.Default.PushoverFailureBodyText;
            set {
                Properties.Settings.Default.PushoverFailureBodyText = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string SmtpFromAddress {
            get => Properties.Settings.Default.SmtpFromAddress;
            set {
                Properties.Settings.Default.SmtpFromAddress = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string SmtpDefaultRecipients {
            get => Properties.Settings.Default.SmtpDefaultRecipients;
            set {
                Properties.Settings.Default.SmtpDefaultRecipients = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string SmtpHostName {
            get => Properties.Settings.Default.SmtpHostName;
            set {
                Properties.Settings.Default.SmtpHostName = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public ushort SmtpHostPort {
            get => Properties.Settings.Default.SmtpHostPort;
            set {
                Properties.Settings.Default.SmtpHostPort = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string SmtpUsername {
            get => Security.Decrypt(Properties.Settings.Default.SmtpUsername);
            set {
                Properties.Settings.Default.SmtpUsername = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string SmtpPassword {
            get => Security.Decrypt(Properties.Settings.Default.SmtpPassword);
            set {
                Properties.Settings.Default.SmtpPassword = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string EmailFailureSubjectText {
            get => Properties.Settings.Default.EmailFailureSubjectText;
            set {
                Properties.Settings.Default.EmailFailureSubjectText = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string EmailFailureBodyText {
            get => Properties.Settings.Default.EmailFailureBodyText;
            set {
                Properties.Settings.Default.EmailFailureBodyText = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string TelegramAccessToken {
            get => Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
            set {
                Properties.Settings.Default.TelegramAccessToken = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string TelegramChatId {
            get => Security.Decrypt(Properties.Settings.Default.TelegramChatId);
            set {
                Properties.Settings.Default.TelegramChatId = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string TelegramFailureBodyText {
            get => Properties.Settings.Default.TelegramFailureBodyText;
            set {
                Properties.Settings.Default.TelegramFailureBodyText = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttBrokerHost {
            get => Properties.Settings.Default.MqttBrokerHost;
            set {
                Properties.Settings.Default.MqttBrokerHost = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public ushort MqttBrokerPort {
            get => Properties.Settings.Default.MqttBrokerPort;
            set {
                Properties.Settings.Default.MqttBrokerPort = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int MqttDefaultQoSLevel {
            get => Properties.Settings.Default.MqttDefaultQoSLevel;
            set {
                Properties.Settings.Default.MqttDefaultQoSLevel = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int MqttDefaultFailureQoSLevel {
            get => Properties.Settings.Default.MqttDefaultFailureQoSLevel;
            set {
                Properties.Settings.Default.MqttDefaultFailureQoSLevel = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttDefaultTopic {
            get => Properties.Settings.Default.MqttDefaultTopic;
            set {
                Properties.Settings.Default.MqttDefaultTopic = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttClientId {
            get => Properties.Settings.Default.MqttClientId;
            set {
                Properties.Settings.Default.MqttClientId = value.Trim();
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool MqttBrokerUseTls {
            get => Properties.Settings.Default.MqttBrokerUseTls;
            set {
                Properties.Settings.Default.MqttBrokerUseTls = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);

                MqttBrokerPort = value ? (ushort)8883 : (ushort)1883;

                RaisePropertyChanged();
            }
        }

        public string MqttUsername {
            get => Security.Decrypt(Properties.Settings.Default.MqttUsername);
            set {
                Properties.Settings.Default.MqttUsername = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttPassword {
            get => Security.Decrypt(Properties.Settings.Default.MqttPassword);
            set {
                Properties.Settings.Default.MqttPassword = Security.Encrypt(value.Trim());
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public int MqttMaxReconnectAttempts {
            get => Properties.Settings.Default.MqttMaxReconnectAttempts;
            set {
                Properties.Settings.Default.MqttMaxReconnectAttempts = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public bool MqttLwtEnable {
            get => Properties.Settings.Default.MqttLwtEnable;
            set {
                Properties.Settings.Default.MqttLwtEnable = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();

                if (!mqttClient.IsConnected && value == true) {
                    LwtStartWorker();
                } else if (mqttClient.IsConnected && value == false) {
                    LwtStopWorker();
                }
            }
        }

        public string MqttLwtTopic {
            get {
                if (string.IsNullOrEmpty(Properties.Settings.Default.MqttLwtTopic)) {
                    Properties.Settings.Default.MqttLwtTopic = Properties.Settings.Default.MqttDefaultTopic;
                }

                return Properties.Settings.Default.MqttLwtTopic;
            }
            set {
                Properties.Settings.Default.MqttLwtTopic = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttLwtBirthPayload {
            get => Properties.Settings.Default.MqttLwtBirthPayload;
            set {
                Properties.Settings.Default.MqttLwtBirthPayload = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttLwtLastWillPayload {
            get => Properties.Settings.Default.MqttLwtLastWillPayload;
            set {
                Properties.Settings.Default.MqttLwtLastWillPayload = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string MqttLwtClosePayload {
            get => Properties.Settings.Default.MqttLwtClosePayload;
            set {
                Properties.Settings.Default.MqttLwtClosePayload = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string TtsTestMessage { get; set; } = "It's full of stars!";

        public string TTSFailureMessage {
            get => Properties.Settings.Default.TTSFailureMessage;
            set {
                Properties.Settings.Default.TTSFailureMessage = value;
                CoreUtil.SaveSettings(Properties.Settings.Default);
                RaisePropertyChanged();
            }
        }

        public IList<string> QoSLevels => MqttCommon.QoSLevels;

        public string TokenDate => DateTime.Now.ToString("d");
        public string TokenTime => DateTime.Now.ToString("T");
        public string TokenDateTime => DateTime.Now.ToString("G");
        public string TokenDateUtc => DateTime.UtcNow.ToString("d");
        public string TokenTimeUtc => DateTime.UtcNow.ToString("T");
        public string TokenDateTimeUtc => DateTime.UtcNow.ToString("G");
        public string TokenUnixEpoch => Utilities.Utilities.UnixEpoch().ToString();

        public void SetSmtpPassword(SecureString s) {
            SmtpPassword = SecureStringToString(s);
        }

        public void SetMqttPassword(SecureString s) {
            MqttPassword = SecureStringToString(s);
        }

        private string SecureStringToString(SecureString value) {
            IntPtr valuePtr = IntPtr.Zero;
            try {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            } finally {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        public static string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}