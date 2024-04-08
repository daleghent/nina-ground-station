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
using Discord.Webhook;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {
    public class DiscordWebhookCommon {
        public readonly AllowedMentions allowedMentions;

        public DiscordWebhookCommon() {
            DiscordWebhookUrl = GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl;
            DiscordWebhookBotName = GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName;
            DiscordImageWebhookUrl = GroundStation.GroundStationConfig.DiscordImageWebhookUrl;

            allowedMentions = new AllowedMentions(AllowedMentionTypes.Everyone | AllowedMentionTypes.Users | AllowedMentionTypes.Roles);
        }

        public async Task SendDiscordWebook(EmbedBuilder embed) {
            try {
                if (string.IsNullOrEmpty(DiscordWebhookUrl)) {
                    throw new Exception("No webhook URL is set");
                }

                using var client = new DiscordWebhookClient(DiscordWebhookUrl);
                await client.SendMessageAsync(username: DiscordWebhookBotName, embeds: new[] { embed.Build() }, allowedMentions: allowedMentions);
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        public async Task SendDiscordImage(ImageData imageData, string fileName, EmbedBuilder embed) {
            try {
                if (string.IsNullOrEmpty(DiscordImageWebhookUrl) && string.IsNullOrEmpty(DiscordWebhookUrl)) {
                    throw new Exception("No webhook URL is set");
                }

                using var client = new DiscordWebhookClient(string.IsNullOrEmpty(DiscordImageWebhookUrl) ? DiscordWebhookUrl : DiscordImageWebhookUrl);
                await client.SendFileAsync(imageData.Bitmap, fileName, string.Empty, username: DiscordWebhookBotName, embeds: new[] { embed.Build() }, allowedMentions: allowedMentions);
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        public List<string> CommonValidation() {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(DiscordWebhookUrl)) {
                errors.Add("Discord webhook URL is not set");
            }

            if (string.IsNullOrEmpty(DiscordWebhookBotName)) {
                errors.Add("Discord webhook bot name is not set");
            }

            return errors;
        }

        private string DiscordWebhookUrl { get; set; }
        private string DiscordImageWebhookUrl { get; set; }
        private string DiscordWebhookBotName { get; set; }
    }
}