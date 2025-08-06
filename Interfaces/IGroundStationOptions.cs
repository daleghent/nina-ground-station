#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.NtfySh;
using DaleGhent.NINA.GroundStation.PushoverClient;
using DaleGhent.NINA.GroundStation.Slack;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DaleGhent.NINA.GroundStation.Interfaces {
    public interface IGroundStationOptions {
        public IList<string> ImageTypes { get; }

        // IFTTT Webhook options
        public string IftttWebhookKey { get; set; }
        public string IftttFailureValue1 { get; set; }
        public string IftttFailureValue2 { get; set; }
        public string IftttFailureValue3 { get; set; }

        // Pushover options
        public string PushoverUserKey { get; set; }
        public string PushoverAppKey { get; set; }
        public NotificationSound PushoverDefaultNotificationSound { get; set; }
        public Priority PushoverDefaultNotificationPriority { get; set; }
        public NotificationSound PushoverDefaultFailureSound { get; set; }
        public Priority PushoverDefaultFailurePriority { get; set; }
        public int PushoverEmergRetryInterval { get; set; }
        public int PushoverEmergExpireAfter { get; set; }
        public string PushoverFailureTitleText { get; set; }
        public string PushoverFailureBodyText { get; set; }
        public Priority[] PushoverPriorities { get; }
        public NotificationSound[] PushoverNotificationSounds { get; }

        // SMTP options
        public string SmtpFromAddress { get; set; }
        public string SmtpDefaultRecipients { get; set; }
        public string SmtpHostName { get; set; }
        public ushort SmtpHostPort { get; set; }
        public string SmtpUsername { get; set; }
        public string SmtpPassword { get; set; }
        public string EmailFailureSubjectText { get; set; }
        public string EmailFailureBodyText { get; set; }

        // Telegram options
        public string TelegramAccessToken { get; set; }
        public string TelegramChatId { get; set; }
        public string TelegramFailureBodyText { get; set; }

        // MQTT options
        public string MqttBrokerHost { get; set; }
        public ushort MqttBrokerPort { get; set; }
        public int MqttDefaultQoSLevel { get; set; }
        public int MqttDefaultFailureQoSLevel { get; set; }
        public string MqttDefaultTopic { get; set; }
        public string MqttClientId { get; set; }
        public bool MqttBrokerUseTls { get; set; }
        public string MqttUsername { get; set; }
        public string MqttPassword { get; set; }
        public int MqttMaxReconnectAttempts { get; set; }
        public bool MqttLwtEnabled { get; set; }
        public string MqttLwtTopic { get; set; }
        public string MqttLwtBirthPayload { get; set; }
        public string MqttLwtLastWillPayload { get; set; }
        public string MqttLwtClosePayload { get; set; }
        public string MqttImageTypesSelected { get; set; }
        public IList<string> MqttQoSLevels { get; }

        // TTS options
        public string TtsTestMessage { get; set; }
        public string TTSFailureMessage { get; set; }

        // Sound Player options
        public string PlaySoundDefaultFile { get; set; }
        public string PlaySoundDefaultFailureFile { get; set; }

        // Discord Webhook options
        public string DiscordWebhookDefaultBotName { get; set; }
        public string DiscordWebhookDefaultUrl { get; set; }
        public string DiscordImageWebhookUrl { get; set; }
        public string DiscordFailureWebhookUrl { get; set; }
        public string DiscordWebhookFailureMessage { get; set; }
        public string DiscordImageTypesSelected { get; set; }

        // slack options
        public string SlackOAuthToken { get; set; }
        public ObservableCollection<Channel> SlackChannels { get; set; }
        public string SlackWorkspaceName { get; set; }
        public string SlackBotName { get; set; }
        public string SlackBotDisplayName { get; set; }
        public string SlackFailureMessage { get; set; }
        public string SlackImageTypesSelected { get; set; }
        public Channel SlackImageEventChannel { get; set; }
        public bool SlackShowChannelInfo { get; set; }
        public string SlackSelectedChannelId { get; set; }
        public bool SlackSelectedChannelIsPrivate { get; set; }
        public int SlackSelectedChannelNumMembers { get; set; }
        public string SlackSelectedChannelCreateDate { get; set; }

        // ntfy.sh options
        public string NtfyShDefaultTopic { get; set; }
        public string NtfyShDefaultIcon { get; set; }
        public string NtfyShUrl { get; set; }
        public string NtfyShUser { get; set; }
        public string NtfyShPassword { get; set; }
        public string NtfyShToken { get; set; }
        public string NtfyShFailureTitle { get; set; }
        public string NtfyShFailureMessage { get; set; }
        public string NtfyShFailureTags { get; set; }
        public NtfyShPriorityLevels NtfyShFailurePriority { get; set; }

        // Image Service options
        public byte ImageServiceFormat { get; set; }
        public byte ImageServiceImageScaling { get; set; }
    }
}
