#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using DaleGhent.NINA.GroundStation.Images;
using DaleGhent.NINA.GroundStation.Interfaces;
using DaleGhent.NINA.GroundStation.Mqtt;
using DaleGhent.NINA.GroundStation.PushoverClient;
using DaleGhent.NINA.GroundStation.TTS;
using DaleGhent.NINA.GroundStation.Utilities;
using Discord;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Media;
using Settings = DaleGhent.NINA.GroundStation.Properties.Settings;

namespace DaleGhent.NINA.GroundStation.Config {

    public partial class GroundStationConfig : BaseINPC, IGroundStationOptions, IDisposable {
        private readonly IProfileService profileService;
        private readonly PluginOptionsAccessor pluginOptionsAccessor;
        private readonly DateTime dateTime;

        public GroundStationConfig(IProfileService profileService) {
            this.profileService = profileService;
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            Guid? guid = PluginOptionsAccessor.GetAssemblyGuid(typeof(GroundStation)) ?? throw new Exception("GUID was not found in assembly metadata");
            pluginOptionsAccessor = new PluginOptionsAccessor(this.profileService, guid.Value);

            if (!Settings.Default.GroundStationMigratedProfiles.Contains(this.profileService.ActiveProfile.Id.ToString()) && !GroundStationProfileMigrated) {
                Logger.Info($"Migrating app settings to NINA profile {this.profileService.ActiveProfile.Name} ({this.profileService.ActiveProfile.Id})");
                MigrateSettingsToProfile();

                GroundStationProfileMigrated = true;
                Settings.Default.GroundStationMigratedProfiles.Add(this.profileService.ActiveProfile.Id.ToString());
                CoreUtil.SaveSettings(Settings.Default);
            }

            dateTime = DateTime.Now;
        }

        public void Dispose() {
            profileService.ProfileChanged -= ProfileService_ProfileChanged;
            GC.SuppressFinalize(this);
        }

        // Profile migration status
        public bool GroundStationProfileMigrated {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(GroundStationProfileMigrated), false);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(GroundStationProfileMigrated), value);
                RaisePropertyChanged();
            }
        }

        //
        // IFTTT Webhook options
        //

        public string IftttWebhookKey {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(IftttWebhookKey), Settings.Default.IFTTTWebhookKey));
            set {
                pluginOptionsAccessor.SetValueString(nameof(IftttWebhookKey), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue1 {
            get => pluginOptionsAccessor.GetValueString(nameof(IftttFailureValue1), Settings.Default.IftttFailureValue1);
            set {
                pluginOptionsAccessor.SetValueString(nameof(IftttFailureValue1), value);
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue2 {
            get => pluginOptionsAccessor.GetValueString(nameof(IftttFailureValue2), Settings.Default.IftttFailureValue2);
            set {
                pluginOptionsAccessor.SetValueString(nameof(IftttFailureValue2), value);
                RaisePropertyChanged();
            }
        }

        public string IftttFailureValue3 {
            get => pluginOptionsAccessor.GetValueString(nameof(IftttFailureValue3), Settings.Default.IftttFailureValue3);
            set {
                pluginOptionsAccessor.SetValueString(nameof(IftttFailureValue3), value);
                RaisePropertyChanged();
            }
        }

        //
        // Pushover options
        //

        public string PushoverUserKey {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(PushoverUserKey), Settings.Default.PushoverUserKey));
            set {
                pluginOptionsAccessor.SetValueString(nameof(PushoverUserKey), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string PushoverAppKey {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(PushoverAppKey), Settings.Default.PushoverAppKey));
            set {
                pluginOptionsAccessor.SetValueString(nameof(PushoverAppKey), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public NotificationSound PushoverDefaultNotificationSound {
            get => (NotificationSound)pluginOptionsAccessor.GetValueUInt16(nameof(PushoverDefaultNotificationSound), (ushort)NotificationSound.Pushover);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(PushoverDefaultNotificationSound), (ushort)value);
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultNotificationPriority {
            get => (Priority)pluginOptionsAccessor.GetValueUInt16(nameof(PushoverDefaultNotificationPriority), (ushort)Priority.Normal);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(PushoverDefaultNotificationPriority), (ushort)value);
                RaisePropertyChanged();
            }
        }

        public NotificationSound PushoverDefaultFailureSound {
            get => (NotificationSound)pluginOptionsAccessor.GetValueUInt16(nameof(PushoverDefaultFailureSound), (ushort)NotificationSound.Pushover);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(PushoverDefaultFailureSound), (ushort)value);
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultFailurePriority {
            get => (Priority)pluginOptionsAccessor.GetValueUInt16(nameof(PushoverDefaultFailurePriority), (ushort)Priority.Normal);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(PushoverDefaultFailurePriority), (ushort)value);
                RaisePropertyChanged();
            }
        }

        public int PushoverEmergRetryInterval {
            get => pluginOptionsAccessor.GetValueInt32(nameof(PushoverEmergRetryInterval), Settings.Default.PushoverEmergRetryInterval);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(PushoverEmergRetryInterval), value);
                RaisePropertyChanged();
            }
        }

        public int PushoverEmergExpireAfter {
            get => pluginOptionsAccessor.GetValueInt32(nameof(PushoverEmergExpireAfter), Settings.Default.PushoverEmergExpireAfter);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(PushoverEmergExpireAfter), value);
                RaisePropertyChanged();
            }
        }

        public string PushoverFailureTitleText {
            get => pluginOptionsAccessor.GetValueString(nameof(PushoverFailureTitleText), Settings.Default.PushoverFailureTitleText);
            set {
                pluginOptionsAccessor.SetValueString(nameof(PushoverFailureTitleText), value);
                RaisePropertyChanged();
            }
        }

        public string PushoverFailureBodyText {
            get => pluginOptionsAccessor.GetValueString(nameof(PushoverFailureBodyText), Settings.Default.PushoverFailureBodyText);
            set {
                pluginOptionsAccessor.SetValueString(nameof(PushoverFailureBodyText), value);
                RaisePropertyChanged();
            }
        }

        public Priority[] PushoverPriorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().ToArray();

        public NotificationSound[] PushoverNotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

        //
        // SMTP options
        //

        public string SmtpFromAddress {
            get => pluginOptionsAccessor.GetValueString(nameof(SmtpFromAddress), Settings.Default.SmtpFromAddress);
            set {
                pluginOptionsAccessor.SetValueString(nameof(SmtpFromAddress), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string SmtpDefaultRecipients {
            get => pluginOptionsAccessor.GetValueString(nameof(SmtpDefaultRecipients), Settings.Default.SmtpDefaultRecipients);
            set {
                pluginOptionsAccessor.SetValueString(nameof(SmtpDefaultRecipients), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string SmtpHostName {
            get => pluginOptionsAccessor.GetValueString(nameof(SmtpHostName), Settings.Default.SmtpHostName);
            set {
                pluginOptionsAccessor.SetValueString(nameof(SmtpHostName), value.Trim());
                RaisePropertyChanged();
            }
        }

        public ushort SmtpHostPort {
            get => pluginOptionsAccessor.GetValueUInt16(nameof(SmtpHostPort), Settings.Default.SmtpHostPort);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(SmtpHostPort), value);
                RaisePropertyChanged();
            }
        }

        public string SmtpUsername {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(SmtpUsername), Settings.Default.SmtpUsername));
            set {
                pluginOptionsAccessor.SetValueString(nameof(SmtpUsername), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string SmtpPassword {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(SmtpPassword), Settings.Default.SmtpPassword));
            set {
                pluginOptionsAccessor.SetValueString(nameof(SmtpPassword), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string EmailFailureSubjectText {
            get => pluginOptionsAccessor.GetValueString(nameof(EmailFailureSubjectText), Settings.Default.EmailFailureSubjectText);
            set {
                pluginOptionsAccessor.SetValueString(nameof(EmailFailureSubjectText), value);
                RaisePropertyChanged();
            }
        }

        public string EmailFailureBodyText {
            get => pluginOptionsAccessor.GetValueString(nameof(EmailFailureBodyText), Settings.Default.EmailFailureBodyText);
            set {
                pluginOptionsAccessor.SetValueString(nameof(EmailFailureBodyText), value);
                RaisePropertyChanged();
            }
        }

        //
        // Telegram options
        //

        public string TelegramAccessToken {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(TelegramAccessToken), Settings.Default.TelegramAccessToken));
            set {
                pluginOptionsAccessor.SetValueString(nameof(TelegramAccessToken), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string TelegramChatId {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(TelegramChatId), Settings.Default.TelegramChatId));
            set {
                pluginOptionsAccessor.SetValueString(nameof(TelegramChatId), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string TelegramFailureBodyText {
            get => pluginOptionsAccessor.GetValueString(nameof(TelegramFailureBodyText), Settings.Default.TelegramFailureBodyText);
            set {
                pluginOptionsAccessor.SetValueString(nameof(TelegramFailureBodyText), value);
                RaisePropertyChanged();
            }
        }

        //
        // MQTT options
        //

        public string MqttBrokerHost {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttBrokerHost), Settings.Default.MqttBrokerHost);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttBrokerHost), value.Trim());
                RaisePropertyChanged();
            }
        }

        public ushort MqttBrokerPort {
            get => pluginOptionsAccessor.GetValueUInt16(nameof(MqttBrokerPort), Settings.Default.MqttBrokerPort);
            set {
                pluginOptionsAccessor.SetValueUInt16(nameof(MqttBrokerPort), value);
                RaisePropertyChanged();
            }
        }

        public int MqttDefaultQoSLevel {
            get => pluginOptionsAccessor.GetValueInt32(nameof(MqttDefaultQoSLevel), Settings.Default.MqttDefaultQoSLevel);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(MqttDefaultQoSLevel), value);
                RaisePropertyChanged();
            }
        }

        public int MqttDefaultFailureQoSLevel {
            get => pluginOptionsAccessor.GetValueInt32(nameof(MqttDefaultFailureQoSLevel), Settings.Default.MqttDefaultFailureQoSLevel);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(MqttDefaultFailureQoSLevel), value);
                RaisePropertyChanged();
            }
        }

        public string MqttDefaultTopic {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttDefaultTopic), Settings.Default.MqttDefaultTopic);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttDefaultTopic), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string MqttClientId {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttClientId), Settings.Default.MqttClientId);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttClientId), value.Trim());
                RaisePropertyChanged();
            }
        }

        public bool MqttBrokerUseTls {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(MqttBrokerUseTls), Settings.Default.MqttBrokerUseTls);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(MqttBrokerUseTls), value);
                MqttBrokerPort = value ? (ushort)8883 : (ushort)1883;
                RaisePropertyChanged();
            }
        }

        public string MqttUsername {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(MqttUsername), Settings.Default.MqttUsername));
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttUsername), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string MqttPassword {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(MqttPassword), Settings.Default.MqttPassword));
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttPassword), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public int MqttMaxReconnectAttempts {
            get => pluginOptionsAccessor.GetValueInt32(nameof(MqttMaxReconnectAttempts), Settings.Default.MqttMaxReconnectAttempts);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(MqttMaxReconnectAttempts), value);
                RaisePropertyChanged();
            }
        }

        public bool MqttLwtEnabled {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(MqttLwtEnabled), Settings.Default.MqttLwtEnable);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(MqttLwtEnabled), value);
                RaisePropertyChanged();

                if (GroundStationProfileMigrated) {
                    if (!GroundStation.LwtIsConnected && value == true) {
                        GroundStation.StartLwtSession();
                    } else if (GroundStation.LwtIsConnected && value == false) {
                        GroundStation.StopLwtSession();
                    }
                }
            }
        }

        public string MqttLwtTopic {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttLwtTopic), Settings.Default.MqttDefaultTopic);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttLwtTopic), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string MqttLwtBirthPayload {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttLwtBirthPayload), Settings.Default.MqttLwtBirthPayload);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttLwtBirthPayload), value);
                RaisePropertyChanged();
            }
        }

        public string MqttLwtLastWillPayload {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttLwtLastWillPayload), Settings.Default.MqttLwtLastWillPayload);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttLwtLastWillPayload), value);
                RaisePropertyChanged();
            }
        }

        public string MqttLwtClosePayload {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttLwtClosePayload), Settings.Default.MqttLwtClosePayload);
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttLwtClosePayload), value);
                RaisePropertyChanged();
            }
        }

        public bool MqttImagePubliserEnabled {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(MqttImagePubliserEnabled), false);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(MqttImagePubliserEnabled), value);
                RaisePropertyChanged();
            }
        }

        public bool MqttImagePubliserMetadataOnly {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(MqttImagePubliserMetadataOnly), false);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(MqttImagePubliserMetadataOnly), value);
                RaisePropertyChanged();
            }
        }

        public string MqttImagePublisherImageTopic {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttImagePublisherImageTopic), "/nina/latestimage/image");
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttImagePublisherImageTopic), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string MqttImagePublisherMetdataTopic {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttImagePublisherMetdataTopic), "/nina/latestimage/metadata");
            set {
                pluginOptionsAccessor.SetValueString(nameof(MqttImagePublisherMetdataTopic), value.Trim());
                RaisePropertyChanged();
            }
        }

        public int MqttImagePublisherQoSLevel {
            get => pluginOptionsAccessor.GetValueInt32(nameof(MqttImagePublisherQoSLevel), MqttDefaultQoSLevel);
            set {
                pluginOptionsAccessor.SetValueInt32(nameof(MqttImagePublisherQoSLevel), value);
                RaisePropertyChanged();
            }
        }

        public string MqttImageTypesSelected {
            get => pluginOptionsAccessor.GetValueString(nameof(MqttImageTypesSelected), DefaultImageTypes());
            set {
                if (!string.IsNullOrEmpty(value)) {
                    pluginOptionsAccessor.SetValueString(nameof(MqttImageTypesSelected), value);
                    RaisePropertyChanged();
                }
            }
        }

        public IList<string> MqttQoSLevels => MqttCommon.QoSLevels;

        //
        // TTS options
        //

        public string TtsTestMessage {
            get => pluginOptionsAccessor.GetValueString(nameof(TtsTestMessage), Settings.Default.TtsTestMessage);
            set {
                pluginOptionsAccessor.SetValueString(nameof(TtsTestMessage), value);
                RaisePropertyChanged();
            }
        }

        public string TTSFailureMessage {
            get => pluginOptionsAccessor.GetValueString(nameof(TTSFailureMessage), Settings.Default.TTSFailureMessage);
            set {
                pluginOptionsAccessor.SetValueString(nameof(TTSFailureMessage), value);
                RaisePropertyChanged();
            }
        }

        public string TtsVoice {
            get => pluginOptionsAccessor.GetValueString(nameof(TtsVoice), TTS.TTS.GetVoiceNames()[0]);
            set {
                pluginOptionsAccessor.SetValueString(nameof(TtsVoice), value);
                RaisePropertyChanged();
            }
        }

        public IList<string> TtsVoices => TTS.TTS.GetVoiceNames();

        //
        // Sound Player options
        //

        public string PlaySoundDefaultFile {
            get => pluginOptionsAccessor.GetValueString(nameof(PlaySoundDefaultFile), Settings.Default.PlaySoundDefaultFile);
            set {
                pluginOptionsAccessor.SetValueString(nameof(PlaySoundDefaultFile), value);
                RaisePropertyChanged();
            }
        }

        public string PlaySoundDefaultFailureFile {
            get => pluginOptionsAccessor.GetValueString(nameof(PlaySoundDefaultFailureFile), Settings.Default.PlaySoundDefaultFailureFile);
            set {
                pluginOptionsAccessor.SetValueString(nameof(PlaySoundDefaultFailureFile), value);
                RaisePropertyChanged();
            }
        }

        //
        // Discord Webhook options
        //

        public string DiscordWebhookDefaultBotName {
            get => pluginOptionsAccessor.GetValueString(nameof(DiscordWebhookDefaultBotName), "🔭 N.I.N.A.");
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordWebhookDefaultBotName), value.Trim());
                RaisePropertyChanged();
            }
        }

        public string DiscordWebhookDefaultUrl {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(DiscordWebhookDefaultUrl), string.Empty));
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordWebhookDefaultUrl), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string DiscordImageWebhookUrl {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(DiscordImageWebhookUrl), string.Empty));
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordImageWebhookUrl), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string DiscordFailureWebhookUrl {
            get => Security.Decrypt(pluginOptionsAccessor.GetValueString(nameof(DiscordFailureWebhookUrl), string.Empty));
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordFailureWebhookUrl), Security.Encrypt(value.Trim()));
                RaisePropertyChanged();
            }
        }

        public string DiscordWebhookFailureMessage {
            get => pluginOptionsAccessor.GetValueString(nameof(DiscordWebhookFailureMessage), Settings.Default.DiscordWebhookFailureMessage);
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordWebhookFailureMessage), value);
                RaisePropertyChanged();
            }
        }

        public bool DiscordImageEventEnabled {
            get => pluginOptionsAccessor.GetValueBoolean(nameof(DiscordImageEventEnabled), false);
            set {
                pluginOptionsAccessor.SetValueBoolean(nameof(DiscordImageEventEnabled), value);
                RaisePropertyChanged();
            }
        }

        public string DiscordImageTypesSelected {
            get => pluginOptionsAccessor.GetValueString(nameof(DiscordImageTypesSelected), DefaultImageTypes());
            set {
                if (!string.IsNullOrEmpty(value)) {
                    pluginOptionsAccessor.SetValueString(nameof(DiscordImageTypesSelected), value);
                    RaisePropertyChanged();
                }
            }
        }

        public IList<string> ImageTypes => Enum.GetValues(typeof(ImageTypesEnum)).Cast<ImageTypesEnum>().Select(x => x.ToString()).ToList();

        public System.Windows.Media.Color DiscordImageEdgeColor {
            get {
                var savedColor = pluginOptionsAccessor.GetValueString(nameof(DiscordImageEdgeColor), Discord.Color.DarkBlue.ToString());
                return (System.Windows.Media.Color)ColorConverter.ConvertFromString(savedColor);
            }
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordImageEdgeColor), value.ToString());
                RaisePropertyChanged();
            }
        }

        public string DiscordImagePostTitle {
            get => pluginOptionsAccessor.GetValueString(nameof(DiscordImagePostTitle), "📡 Ground Station");
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordImagePostTitle), value);
                RaisePropertyChanged();
            }
        }

        public System.Windows.Media.Color DiscordMessageEdgeColor {
            get {
                var savedColor = pluginOptionsAccessor.GetValueString(nameof(DiscordMessageEdgeColor), Discord.Color.DarkBlue.ToString());
                return (System.Windows.Media.Color)ColorConverter.ConvertFromString(savedColor);
            }
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordMessageEdgeColor), value.ToString());
                RaisePropertyChanged();
            }
        }

        public System.Windows.Media.Color DiscordFailureMessageEdgeColor {
            get {
                var savedColor = pluginOptionsAccessor.GetValueString(nameof(DiscordFailureMessageEdgeColor), Discord.Color.Red.ToString());
                return (System.Windows.Media.Color)ColorConverter.ConvertFromString(savedColor);
            }
            set {
                pluginOptionsAccessor.SetValueString(nameof(DiscordFailureMessageEdgeColor), value.ToString());
                RaisePropertyChanged();
            }
        }

        //
        // Image Service options
        //

        public byte ImageServiceFormat {
            get => pluginOptionsAccessor.GetValueByte(nameof(ImageServiceFormat), (byte)ImageFormatEnum.PNG);
            set {
                pluginOptionsAccessor.SetValueByte(nameof(ImageServiceFormat), value);
                RaisePropertyChanged();
            }
        }

        public byte ImageServiceImageScaling {
            get => pluginOptionsAccessor.GetValueByte(nameof(ImageServiceImageScaling), 100);
            set {
                if (value < 10) { value = 10; }
                if (value > 100) { value = 100; }

                pluginOptionsAccessor.SetValueByte(nameof(ImageServiceImageScaling), value);
                RaisePropertyChanged();
            }
        }

        public static ImageFormatEnum[] ImageServiceFormats => Enum.GetValues(typeof(ImageFormatEnum)).Cast<ImageFormatEnum>().ToArray();

        //
        // Utility methods
        //

        public void SetSmtpPassword(SecureString s) {
            SmtpPassword = SecureStringToString(s);
        }

        public void SetMqttPassword(SecureString s) {
            MqttPassword = SecureStringToString(s);
        }

        private static string SecureStringToString(SecureString value) {
            IntPtr valuePtr = IntPtr.Zero;
            try {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);
                return Marshal.PtrToStringUni(valuePtr);
            } finally {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        [RelayCommand]
        private static async Task<bool> PushoverTest(object arg) {
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

        [RelayCommand]
        private static async Task<bool> EmailTest(object arg) {
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

        [RelayCommand]
        private static async Task<bool> TelegramTest(object arg) {
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

        [RelayCommand]
        private static async Task<bool> MQTTTest(object arg) {
            var send = new SendToMqtt.SendToMqtt() {
                Topic = "/nina/test",
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

        [RelayCommand]
        private static async Task<bool> IftttTest(object arg) {
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

        [RelayCommand]
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

        [RelayCommand]
        private static async Task<bool> DiscordWebhookTest(object arg) {
            var embed = new EmbedBuilder() {
                Title = "Test message title",
                Description = "This is a test message description",
            };

            try {
                var send = new DiscordWebhook.DiscordWebhookCommon();
                await send.SendDiscordWebook(embed);
            } catch (Exception ex) {
                Notification.ShowExternalError($"Failed to send message to Discord Webhook:{Environment.NewLine}{ex.Message}", "Discord Webhook Error");
                return false;
            }

            return true;
        }

        [RelayCommand]
        internal void OpenSelectDefaultSoundFileDialog(object obj) {
            Microsoft.Win32.OpenFileDialog dialog = new() {
                FileName = string.Empty,
                Filter = PlaySound.PlaySoundCommon.FileTypeFilter,
            };

            if (dialog.ShowDialog() == true) {
                PlaySoundDefaultFile = dialog.FileName;
            }
        }

        [RelayCommand]
        internal void OpenSelectDefaultFailureSoundFileDialog(object obj) {
            Microsoft.Win32.OpenFileDialog dialog = new() {
                FileName = string.Empty,
                Filter = PlaySound.PlaySoundCommon.FileTypeFilter,
            };

            if (dialog.ShowDialog() == true) {
                PlaySoundDefaultFailureFile = dialog.FileName;
            }
        }

        private static string DefaultImageTypes() {
            return string.Join(',', [ImageTypesEnum.SNAPSHOT.ToString(), ImageTypesEnum.LIGHT.ToString()]);
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            RaiseAllPropertiesChanged();
        }

        private void MigrateSettingsToProfile() {
            IftttWebhookKey = Security.Decrypt(Settings.Default.IFTTTWebhookKey);
            IftttFailureValue1 = Settings.Default.IftttFailureValue1;
            IftttFailureValue2 = Settings.Default.IftttFailureValue2;
            IftttFailureValue3 = Settings.Default.IftttFailureValue3;

            PushoverUserKey = Security.Decrypt(Settings.Default.PushoverUserKey);
            PushoverAppKey = Security.Decrypt(Settings.Default.PushoverAppKey);
            PushoverDefaultNotificationSound = Enum.Parse<NotificationSound>(Settings.Default.PushoverDefaultNotificationSound);
            PushoverDefaultNotificationPriority = Enum.Parse<Priority>(Settings.Default.PushoverDefaultNotificationPriority);
            PushoverDefaultFailureSound = Enum.Parse<NotificationSound>(Settings.Default.PushoverDefaultFailureSound);
            PushoverDefaultFailurePriority = Enum.Parse<Priority>(Settings.Default.PushoverDefaultFailurePriority);
            PushoverEmergRetryInterval = Settings.Default.PushoverEmergRetryInterval;
            PushoverEmergExpireAfter = Settings.Default.PushoverEmergExpireAfter;
            PushoverFailureTitleText = Settings.Default.PushoverFailureTitleText;
            PushoverFailureBodyText = Settings.Default.PushoverFailureBodyText;

            SmtpFromAddress = Settings.Default.SmtpFromAddress;
            SmtpDefaultRecipients = Settings.Default.SmtpDefaultRecipients;
            SmtpHostName = Settings.Default.SmtpHostName;
            SmtpHostPort = Settings.Default.SmtpHostPort;
            SmtpUsername = Security.Decrypt(Settings.Default.SmtpUsername);
            SmtpPassword = Security.Decrypt(Settings.Default.SmtpPassword);
            EmailFailureSubjectText = Settings.Default.EmailFailureSubjectText;
            EmailFailureBodyText = Settings.Default.EmailFailureBodyText;

            TelegramAccessToken = Security.Decrypt(Settings.Default.TelegramAccessToken);
            TelegramChatId = Security.Decrypt(Settings.Default.TelegramChatId);
            TelegramFailureBodyText = Settings.Default.TelegramFailureBodyText;

            MqttBrokerHost = Settings.Default.MqttBrokerHost;
            MqttBrokerPort = Settings.Default.MqttBrokerPort;
            MqttDefaultQoSLevel = Settings.Default.MqttDefaultQoSLevel;
            MqttDefaultFailureQoSLevel = Settings.Default.MqttDefaultFailureQoSLevel;
            MqttDefaultTopic = Settings.Default.MqttDefaultTopic;
            MqttClientId = Settings.Default.MqttClientId;
            MqttBrokerUseTls = Settings.Default.MqttBrokerUseTls;
            MqttUsername = Security.Decrypt(Settings.Default.MqttUsername);
            MqttPassword = Security.Decrypt(Settings.Default.MqttPassword);
            MqttMaxReconnectAttempts = Settings.Default.MqttMaxReconnectAttempts;
            MqttLwtEnabled = Settings.Default.MqttLwtEnable;
            MqttLwtTopic = Settings.Default.MqttLwtTopic;
            MqttLwtBirthPayload = Settings.Default.MqttLwtBirthPayload;
            MqttLwtLastWillPayload = Settings.Default.MqttLwtLastWillPayload;
            MqttLwtClosePayload = Settings.Default.MqttLwtClosePayload;

            TtsTestMessage = Settings.Default.TtsTestMessage;
            TTSFailureMessage = Settings.Default.TTSFailureMessage;

            PlaySoundDefaultFile = Settings.Default.PlaySoundDefaultFile;
            PlaySoundDefaultFailureFile = Settings.Default.PlaySoundDefaultFailureFile;
        }

        public string TokenDate => dateTime.ToString("d");
        public string TokenTime => dateTime.ToString("T");
        public string TokenDateTime => dateTime.ToString("G");
        public string TokenDateUtc => dateTime.ToUniversalTime().ToString("d");
        public string TokenTimeUtc => dateTime.ToUniversalTime().ToString("T");
        public string TokenDateTimeUtc => dateTime.ToUniversalTime().ToString("G");
        public string TokenUnixEpoch => Utilities.Utilities.UnixEpoch(dateTime).ToString();
    }
}