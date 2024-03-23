#region "copyright"

/*
    Copyright 2023 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using Discord;
using Discord.Webhook;
using System;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {
    public class DiscordWebhookCommon {

        public DiscordWebhookCommon() {
            DiscordWebhookUrl = GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl;
            DiscordWebhookBotName = GroundStation.GroundStationConfig.DiscordWebhookDefaultBotName;
            DiscordImageWebhookUrl = GroundStation.GroundStationConfig.DiscordImageWebhookUrl;
        }

        public async Task SendDiscordWebook(EmbedBuilder embed) {
            try {
                if (string.IsNullOrEmpty(DiscordWebhookUrl)) {
                    throw new Exception("No webhook URL is set");
                }

                using var client = new DiscordWebhookClient(DiscordWebhookUrl);
                await client.SendMessageAsync(username: DiscordWebhookBotName, embeds: new[] { embed.Build() });
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
                await client.SendFileAsync(imageData.PngBitMap, fileName, string.Empty, username: DiscordWebhookBotName, embeds: new[] { embed.Build() });
                client.Dispose();
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}");
            }
        }

        private string DiscordWebhookUrl { get; set; }
        private string DiscordImageWebhookUrl { get; set; }
        private string DiscordWebhookBotName { get; set; }
    }
}