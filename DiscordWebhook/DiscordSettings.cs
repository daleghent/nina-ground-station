#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    internal interface IDiscordRuntimeSettings {
        string BotToken { get; }
        string WebhookDefaultUrl { get; }
        string WebhookDefaultBotName { get; }
        bool UseSessionThreads { get; }
        string ThreadNameTemplate { get; }
        string FailureWebhookUrl { get; }
        string ImageWebhookUrl { get; }
        TimeSpan SessionRolloverTimeSpan { get; }
    }

    internal sealed class PluginDiscordRuntimeSettings : IDiscordRuntimeSettings {
        public string BotToken => GroundStation.GroundStationConfig?.DiscordBotToken;
        public string WebhookDefaultUrl => GroundStation.GroundStationConfig?.DiscordWebhookDefaultUrl;
        public string WebhookDefaultBotName => GroundStation.GroundStationConfig?.DiscordWebhookDefaultBotName;
        public bool UseSessionThreads => GroundStation.GroundStationConfig?.DiscordUseSessionThreads ?? false;
        public string ThreadNameTemplate => GroundStation.GroundStationConfig?.DiscordThreadNameTemplate;
        public string FailureWebhookUrl => GroundStation.GroundStationConfig?.DiscordFailureWebhookUrl;
        public string ImageWebhookUrl => GroundStation.GroundStationConfig?.DiscordImageWebhookUrl;
        public TimeSpan SessionRolloverTimeSpan => GroundStation.GroundStationConfig?.SessionRolloverTimeSpan ?? TimeSpan.FromHours(16);
    }

    internal static class DiscordSettings {
        internal static IDiscordRuntimeSettings Current { get; set; } = new PluginDiscordRuntimeSettings();

        internal static void Reset() {
            Current = new PluginDiscordRuntimeSettings();
        }
    }
}
