using DaleGhent.NINA.GroundStation.DiscordWebhook;
using System;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    internal sealed class FakeDiscordSettings : IDiscordRuntimeSettings {
        public string BotToken { get; set; } = "bot-token";
        public string WebhookDefaultUrl { get; set; } = FakeDiscordApi.WebhookUrl;
        public string WebhookDefaultBotName { get; set; } = "Ground Station";
        public bool UseSessionThreads { get; set; } = true;
        public string ThreadNameTemplate { get; set; } = "test-session";
        public string FailureWebhookUrl { get; set; } = string.Empty;
        public string ImageWebhookUrl { get; set; } = string.Empty;
        public TimeSpan SessionRolloverTimeSpan { get; set; } = TimeSpan.FromHours(16);
    }
}
