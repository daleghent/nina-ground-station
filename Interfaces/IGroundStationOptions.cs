using DaleGhent.NINA.GroundStation.Images;
using DaleGhent.NINA.GroundStation.PushoverClient;
using System.Collections.Generic;

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
    }
}
