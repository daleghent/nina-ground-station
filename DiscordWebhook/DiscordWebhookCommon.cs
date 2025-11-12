#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using Discord;
using Discord.Rest;
using Discord.Webhook;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {
    public class DiscordWebhookCommon {
        private readonly AllowedMentions allowedMentions;
        private readonly DiscordRestConfig discordRestConfig;

        public DiscordWebhookCommon() {
            discordRestConfig = new() {
                DefaultRetryMode = RetryMode.AlwaysRetry,
            };

            allowedMentions = new(AllowedMentionTypes.Everyone | AllowedMentionTypes.Users | AllowedMentionTypes.Roles);
        }

        public async Task SendDiscordWebhook(string text, bool isFailure = false) {
            try {
                var webhook = GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl;

                if (isFailure && !string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordFailureWebhookUrl)) {
                    webhook = GroundStation.GroundStationConfig.DiscordFailureWebhookUrl;
                }

                using var client = new DiscordWebhookClient(GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl, discordRestConfig);

                await client.SendMessageAsync(text, username: GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName, allowedMentions: allowedMentions);
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        public async Task SendDiscordWebhook(string message, IList<Embed> embeds, bool isFailure = false) {
            try {
                var webhook = GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl;

                if (isFailure && !string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordFailureWebhookUrl)) {
                    webhook = GroundStation.GroundStationConfig.DiscordFailureWebhookUrl;
                }

                using var client = new DiscordWebhookClient(webhook, discordRestConfig);

                await client.SendMessageAsync(message, username: GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName, embeds: embeds, allowedMentions: allowedMentions);
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        public async Task SendDiscordImage(ImageData imageData, string fileName, IList<Embed> embeds) {
            try {
                if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordImageWebhookUrl) && string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl)) {
                    throw new Exception("No webhook URL is set");
                }

                string webhookUrl = string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordImageWebhookUrl) ?
                    GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl : GroundStation.GroundStationConfig.DiscordImageWebhookUrl;

                using var client = new DiscordWebhookClient(webhookUrl, discordRestConfig);

                await client.SendFileAsync(imageData.Bitmap, fileName, string.Empty, username: GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName, embeds: embeds, allowedMentions: allowedMentions);
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        public static List<string> CommonValidation() {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl)) {
                errors.Add("Discord webhook URL is not set");
            }

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName)) {
                errors.Add("Discord webhook bot name is not set");
            }

            return errors;
        }
    }
}