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
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GSUtilities = DaleGhent.NINA.GroundStation.Utilities.Utilities;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {
    public class DiscordWebhookCommon {
        private readonly DiscordClient discordClient;
        private readonly DiscordSessionThreads sessionThreads;

        private static readonly string[] allowedMentionTypes = new[] { "everyone", "users", "roles" };

        public DiscordWebhookCommon() : this(new DiscordClient()) {
        }

        internal DiscordWebhookCommon(DiscordClient discordClient) {
            this.discordClient = discordClient;
            sessionThreads = new DiscordSessionThreads(discordClient);
        }

        public async Task SendDiscordWebhook(string text, bool isFailure = false, bool bypassSessionThread = false) {
            try {
                await SendWebhookPayload(isFailure ? DiscordWebhookRoute.Failure : DiscordWebhookRoute.Default, text, null, bypassSessionThread: bypassSessionThread);
            } catch (DiscordApiException) {
                throw;
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}", ex);
            }
        }

        public async Task SendDiscordWebhook(string message, IList<Embed> embeds, bool isFailure = false, bool bypassSessionThread = false) {
            try {
                await SendWebhookPayload(isFailure ? DiscordWebhookRoute.Failure : DiscordWebhookRoute.Default, message, embeds, bypassSessionThread: bypassSessionThread);
            } catch (DiscordApiException) {
                throw;
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}", ex);
            }
        }

        public async Task SendDiscordImage(ImageData imageData, string fileName, IList<Embed> embeds) {
            try {
                if (string.IsNullOrEmpty(DiscordSettings.Current.ImageWebhookUrl) && string.IsNullOrEmpty(DiscordSettings.Current.WebhookDefaultUrl)) {
                    throw new Exception("No webhook URL is set");
                }

                await SendWebhookPayload(DiscordWebhookRoute.Image, string.Empty, embeds, imageData.Bitmap, fileName);
            } catch (DiscordApiException) {
                throw;
            } catch (Exception ex) {
                throw new Exception($"Failed to send Discord webhook: {ex.Message}", ex);
            }
        }

        private async Task SendWebhookPayload(DiscordWebhookRoute route, string message, IList<Embed> embeds, Stream attachmentStream = null, string attachmentFileName = null, bool bypassSessionThread = false) {
            var webhookUrl = GetWebhookUrl(route);
            var webhookMetadata = await discordClient.GetWebhookMetadata(webhookUrl);
            var hasBotToken = !string.IsNullOrWhiteSpace(DiscordSettings.Current.BotToken);
            var useSessionThreads = DiscordSettings.Current.UseSessionThreads;

            Logger.Debug($"Discord send start route={route} useSessionThreads={useSessionThreads} bypassSessionThread={bypassSessionThread} hasBotToken={hasBotToken} parentChannelId='{webhookMetadata.ChannelId}' guildId='{webhookMetadata.GuildId}'");

            if (bypassSessionThread || !useSessionThreads) {
                await SendValidatedWebhookMessage(webhookUrl, message, embeds, attachmentStream, attachmentFileName, expectedChannelId: webhookMetadata.ChannelId);
                Logger.Info($"Discord send route={route} posted directly to parent channel '{webhookMetadata.ChannelId}' bypassSessionThread={bypassSessionThread} useSessionThreads={useSessionThreads}");
                return;
            }

            // Serialized so that concurrent sends cannot each create or reconcile the same session thread.
            await sessionThreads.ExecuteSerialized(async () => {
                var threadKey = GetThreadKey(DateTime.Now);

                if (!hasBotToken) {
                    Logger.Info($"Discord send route={route} attempting native webhook thread behavior without a bot token");

                    try {
                        var nativeResponse = await ExecuteWebhookRequest(webhookUrl, message, embeds, attachmentStream, attachmentFileName, waitForResponse: true, threadName: threadKey, notifyOnFailure: false);
                        var nativeChannelId = DiscordClient.GetRequiredString(nativeResponse, "channel_id", "Discord Webhook Error", "Discord did not return a channel id in the webhook response.", notifyOnFailure: false);

                        if (!string.Equals(nativeChannelId, webhookMetadata.ChannelId, StringComparison.Ordinal)) {
                            sessionThreads.ValidateWebhookMessage(nativeResponse, nativeChannelId, "Discord Webhook Error", "Discord returned an invalid webhook thread response.", notifyOnFailure: false);
                            Logger.Info($"Discord send route={route} posted through native webhook thread behavior to thread '{nativeChannelId}'");
                            return;
                        }

                        sessionThreads.ValidateWebhookMessage(nativeResponse, webhookMetadata.ChannelId, "Discord Webhook Error", "Discord returned an invalid webhook message response.", notifyOnFailure: false);
                        Logger.Info($"Discord send route={route} used plain webhook behavior because the native webhook thread request posted to the parent channel");
                        return;
                    } catch (DiscordApiException ex) when (DiscordSessionThreads.CanFallBackToPlainWebhook(ex)) {
                        Logger.Info($"Discord send route={route} falling back to plain webhook behavior because native webhook thread creation is unsupported for this channel type");
                        await SendValidatedWebhookMessage(webhookUrl, message, embeds, attachmentStream, attachmentFileName, expectedChannelId: webhookMetadata.ChannelId);
                        return;
                    }
                }

                var parentChannel = await discordClient.GetChannel(webhookMetadata.ChannelId);
                sessionThreads.ValidateParentChannel(parentChannel, webhookMetadata.ChannelId);
                var usesBotManagedTextThread = DiscordClient.RequiresBotThreadCreation(parentChannel);
                var shouldMarkFailureSeedMessage = route == DiscordWebhookRoute.Failure
                    && string.IsNullOrWhiteSpace(DiscordSettings.Current.FailureWebhookUrl)
                    && usesBotManagedTextThread;
                Logger.Debug($"Discord send route={route} resolved threadKey='{threadKey}'");

                if (usesBotManagedTextThread) {
                    var botManagedThread = await sessionThreads.ResolveBotManagedTextThread(webhookMetadata, threadKey);
                    await SendValidatedWebhookMessage(webhookUrl, message, embeds, attachmentStream, attachmentFileName, expectedChannelId: botManagedThread.ThreadId, threadId: botManagedThread.ThreadId);
                    if (shouldMarkFailureSeedMessage) {
                        await sessionThreads.TryMarkThreadStarterMessageAsFailed(webhookMetadata, botManagedThread.ThreadId, threadKey, botManagedThread.CanEditFailureSeed);
                    }
                    Logger.Info($"Discord send route={route} posted to session thread '{botManagedThread.ThreadId}'");
                    return;
                }

                var nonBotExistingThread = await sessionThreads.FindActiveThreadByName(webhookMetadata.GuildId, webhookMetadata.ChannelId, threadKey);
                if (nonBotExistingThread != null) {
                    var existingThreadId = DiscordClient.GetRequiredString(nonBotExistingThread, "id", "Discord Bot Error", $"Discord returned an active thread without an id for name '{threadKey}'.");
                    Logger.Info($"Discord send route={route} resolved active thread '{existingThreadId}' by name '{threadKey}'");
                    await SendValidatedWebhookMessage(webhookUrl, message, embeds, attachmentStream, attachmentFileName, expectedChannelId: existingThreadId, threadId: existingThreadId);
                    Logger.Info($"Discord send route={route} posted to active thread '{existingThreadId}'");
                    return;
                }

                var responseJson = await ExecuteWebhookRequest(webhookUrl, message, embeds, attachmentStream, attachmentFileName, waitForResponse: true, threadName: threadKey);
                var createdThreadId = DiscordClient.GetRequiredString(responseJson, "channel_id", "Discord Webhook Error", "Discord did not return a thread id in the webhook response.");

                if (createdThreadId == webhookMetadata.ChannelId) {
                    throw DiscordClient.CreateApiException("Discord Webhook Error", "Discord accepted the webhook message but did not create a thread. A normal text channel requires a bot token to create new session threads.");
                }

                sessionThreads.ValidateWebhookMessage(responseJson, createdThreadId, "Discord Webhook Error", "Discord returned an invalid forum/media thread webhook response.");
                var createdThread = await discordClient.GetChannel(createdThreadId);
                sessionThreads.ValidateThreadChannel(createdThread, webhookMetadata.ChannelId, "Discord Webhook Error", "Discord returned an invalid thread after webhook thread creation.");
                await sessionThreads.ValidateActiveThreadInGuild(webhookMetadata.GuildId, createdThreadId, webhookMetadata.ChannelId, "Discord Webhook Error", "Discord did not report the created thread as active.");
                Logger.Info($"Discord send route={route} created forum/media thread '{createdThreadId}'");
            });
        }

        private async Task<JObject> ExecuteWebhookRequest(string webhookUrl, string message, IList<Embed> embeds, Stream attachmentStream, string attachmentFileName, bool waitForResponse = false, string threadId = null, string threadName = null, bool notifyOnFailure = true) {
            // Buffered up front because a rate-limit retry rebuilds the request, and the
            // previous attempt's disposal would have taken the source stream with it.
            byte[] attachmentBytes = null;
            if (attachmentStream != null) {
                attachmentStream.Position = 0;
                using var buffer = new MemoryStream();
                await attachmentStream.CopyToAsync(buffer);
                attachmentBytes = buffer.ToArray();
            }

            var payload = new Dictionary<string, object> {
                ["content"] = message,
                ["username"] = DiscordSettings.Current.WebhookDefaultBotName,
                ["allowed_mentions"] = new {
                    parse = allowedMentionTypes,
                },
            };

            if (embeds != null) {
                payload["embeds"] = embeds.Select(ToDiscordPayload).ToArray();
            }

            // thread_name must be omitted unless we are creating a forum/media post.
            // Sending "thread_name": null on a text channel can attach the message to
            // the active session thread instead of the parent channel.
            if (!string.IsNullOrWhiteSpace(threadName)) {
                payload["thread_name"] = threadName;
            }

            return await discordClient.PostWebhook(webhookUrl, payload, waitForResponse, threadId, attachmentBytes, attachmentFileName, notifyOnFailure);
        }

        private async Task<JObject> SendValidatedWebhookMessage(string webhookUrl, string message, IList<Embed> embeds, Stream attachmentStream, string attachmentFileName, string expectedChannelId, string threadId = null, string threadName = null, bool notifyOnFailure = true) {
            var responseJson = await ExecuteWebhookRequest(webhookUrl, message, embeds, attachmentStream, attachmentFileName, waitForResponse: true, threadId: threadId, threadName: threadName, notifyOnFailure: notifyOnFailure);
            sessionThreads.ValidateWebhookMessage(responseJson, expectedChannelId, "Discord Webhook Error", "Discord returned an invalid webhook message response.", notifyOnFailure);
            Logger.Debug($"Discord webhook post validated expectedChannelId='{expectedChannelId}' responseMessageId='{responseJson?["id"]?.Value<string>()}' responseChannelId='{responseJson?["channel_id"]?.Value<string>()}' threadId='{threadId}' threadName='{threadName}'");
            return responseJson;
        }

        private object ToDiscordPayload(Embed embed) {
            return new {
                title = embed.Title,
                description = embed.Description,
                url = embed.Url,
                timestamp = embed.Timestamp?.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
                color = embed.Color?.RawValue,
                author = embed.Author == null ? null : new {
                    name = embed.Author.Value.Name,
                    url = embed.Author.Value.Url,
                    icon_url = embed.Author.Value.IconUrl,
                },
                footer = embed.Footer == null ? null : new {
                    text = embed.Footer.Value.Text,
                    icon_url = embed.Footer.Value.IconUrl,
                },
                fields = embed.Fields.Select(field => new {
                    name = field.Name,
                    value = field.Value,
                    inline = field.Inline,
                }).ToArray(),
                image = embed.Image == null ? null : new {
                    url = embed.Image.Value.Url,
                },
                thumbnail = embed.Thumbnail == null ? null : new {
                    url = embed.Thumbnail.Value.Url,
                },
            };
        }

        private string GetWebhookUrl(DiscordWebhookRoute route) {
            return route switch {
                DiscordWebhookRoute.Failure when !string.IsNullOrWhiteSpace(DiscordSettings.Current.FailureWebhookUrl) => DiscordSettings.Current.FailureWebhookUrl,
                DiscordWebhookRoute.Image when !string.IsNullOrWhiteSpace(DiscordSettings.Current.ImageWebhookUrl) => DiscordSettings.Current.ImageWebhookUrl,
                _ => DiscordSettings.Current.WebhookDefaultUrl,
            };
        }

        internal string GetThreadKey(DateTime now) {
            var template = DiscordSettings.Current.ThreadNameTemplate;
            return GSUtilities.ResolveTokens(template, nowOverride: now).Trim();
        }

        public static List<string> CommonValidation() {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(DiscordSettings.Current.WebhookDefaultUrl)) {
                errors.Add("Discord webhook URL is not set");
            }

            if (string.IsNullOrEmpty(DiscordSettings.Current.WebhookDefaultBotName)) {
                errors.Add("Discord webhook bot name is not set");
            }

            return errors;
        }

        private enum DiscordWebhookRoute {
            Default,
            Failure,
            Image,
        }
    }
}
