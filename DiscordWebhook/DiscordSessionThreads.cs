#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    internal sealed class DiscordSessionThreads {
        private readonly DiscordClient discordClient;
        private static readonly SemaphoreSlim sessionThreadLock = new(1, 1);
        private static readonly string[] allowedMentionTypes = new[] { "everyone", "users", "roles" };

        private const int AnnouncementThreadChannelType = 10;
        private const int PublicThreadChannelType = 11;
        private const int PrivateThreadChannelType = 12;
        private const int DefaultAutoArchiveDurationMinutes = 1440;
        private static readonly TimeSpan VisibleParentScanWindow = TimeSpan.FromMinutes(DefaultAutoArchiveDurationMinutes);

        internal DiscordSessionThreads(DiscordClient discordClient) {
            this.discordClient = discordClient;
        }

        internal async Task ExecuteSerialized(Func<Task> action) {
            await sessionThreadLock.WaitAsync();
            try {
                await action();
            } finally {
                sessionThreadLock.Release();
            }
        }

        internal static bool CanFallBackToPlainWebhook(DiscordApiException ex) {
            return Regex.IsMatch(ex.Message, "\"code\":\\s*220003")
                || ex.Message.Contains("Webhooks can only create threads in forum channels", StringComparison.Ordinal);
        }

        internal void ValidateWebhookMessage(JObject message, string expectedChannelId, string title, string failureMessage, bool notifyOnFailure = true) {
            var messageId = DiscordClient.GetRequiredString(message, "id", title, $"{failureMessage} Discord did not return a message id.", notifyOnFailure);
            var messageChannelId = DiscordClient.GetRequiredString(message, "channel_id", title, $"{failureMessage} Discord did not return a channel id for message {messageId}.", notifyOnFailure);

            if (!string.Equals(messageChannelId, expectedChannelId, StringComparison.Ordinal)) {
                throw DiscordClient.CreateApiException(title, $"{failureMessage} Expected channel {expectedChannelId}, but Discord returned channel {messageChannelId} for message {messageId}.", notifyUser: notifyOnFailure);
            }
        }

        internal void ValidateParentChannel(JObject channel, string expectedChannelId, bool notifyOnFailure = true) {
            var channelId = DiscordClient.GetRequiredString(channel, "id", "Discord Bot Error", "Discord did not return an id for the webhook channel.", notifyOnFailure);
            if (!string.Equals(channelId, expectedChannelId, StringComparison.Ordinal)) {
                throw DiscordClient.CreateApiException("Discord Bot Error", $"Discord returned webhook channel {channelId}, but expected {expectedChannelId}.", notifyUser: notifyOnFailure);
            }

            _ = channel["type"]?.Value<int>()
                ?? throw DiscordClient.CreateApiException("Discord Bot Error", "Discord did not return a channel type for the webhook channel.", notifyUser: notifyOnFailure);
        }

        internal void ValidateThreadChannel(JObject channel, string expectedParentChannelId, string title, string failureMessage, bool notifyOnFailure = true) {
            var channelId = DiscordClient.GetRequiredString(channel, "id", title, $"{failureMessage} Discord did not return a thread id.", notifyOnFailure);
            var channelType = channel["type"]?.Value<int>()
                ?? throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord did not return a thread channel type for {channelId}.", notifyUser: notifyOnFailure);

            if (!IsThreadChannelType(channelType)) {
                throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord returned channel {channelId} with non-thread type {channelType}.", notifyUser: notifyOnFailure);
            }

            var parentId = DiscordClient.GetRequiredString(channel, "parent_id", title, $"{failureMessage} Discord did not return a parent channel for thread {channelId}.", notifyOnFailure);
            if (!string.Equals(parentId, expectedParentChannelId, StringComparison.Ordinal)) {
                throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord returned parent channel {parentId} for thread {channelId}, expected {expectedParentChannelId}.", notifyUser: notifyOnFailure);
            }

            var threadMetadata = channel["thread_metadata"] as JObject
                ?? throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord did not return thread metadata for thread {channelId}.", notifyUser: notifyOnFailure);

            var isArchived = threadMetadata["archived"]?.Value<bool>() ?? true;
            var isLocked = threadMetadata["locked"]?.Value<bool>() ?? false;

            if (isArchived || isLocked) {
                throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord thread {channelId} is archived or locked.", notifyUser: notifyOnFailure);
            }
        }

        internal async Task ValidateActiveThreadInGuild(string guildId, string threadId, string expectedParentChannelId, string title, string failureMessage, bool notifyOnFailure = true) {
            var response = await discordClient.SendBotRequest(HttpMethod.Get, $"https://discord.com/api/v10/guilds/{guildId}/threads/active", notifyOnFailure: notifyOnFailure);
            var threads = response["threads"] as JArray
                ?? throw DiscordClient.CreateApiException(title, $"{failureMessage} Discord did not return an active thread list for guild {guildId}.", notifyUser: notifyOnFailure);

            Logger.Debug($"Discord active-thread validation guildId='{guildId}' threadId='{threadId}' activeThreadCount={threads.Count}");

            var matchingThread = threads
                .OfType<JObject>()
                .FirstOrDefault(thread => string.Equals(thread["id"]?.Value<string>(), threadId, StringComparison.Ordinal));

            if (matchingThread == null) {
                throw DiscordClient.CreateApiException(title, $"{failureMessage} Thread {threadId} is not present in the guild's active thread list.", notifyUser: notifyOnFailure);
            }

            ValidateThreadChannel(matchingThread, expectedParentChannelId, title, failureMessage, notifyOnFailure);
        }

        internal async Task<BotManagedThreadResolution> ResolveBotManagedTextThread(WebhookMetadata webhookMetadata, string threadName) {
            var visibleEntries = await GetVisibleParentEntries(webhookMetadata);
            visibleEntries = await PurgeUnattachedFailedSeedMessages(webhookMetadata, visibleEntries);
            var candidateThreads = await GetOrphanCleanupCandidates(webhookMetadata.GuildId, webhookMetadata.ChannelId);
            var existingThread = await ResolveActiveThreadFromSnapshot(candidateThreads, visibleEntries, webhookMetadata, threadName);
            var reusableStarterMessage = FindReusableStarterMessage(visibleEntries, webhookMetadata.ChannelId, threadName);

            if (existingThread != null && reusableStarterMessage != null && string.Equals(existingThread["id"]?.Value<string>(), reusableStarterMessage.ThreadId, StringComparison.Ordinal)) {
                if (reusableStarterMessage.DuplicateMessageIdsToDelete.Length > 0) {
                    await TryDeleteDuplicateParentMessages(webhookMetadata, reusableStarterMessage.DuplicateMessageIdsToDelete, threadName);
                }

                var existingThreadId = DiscordClient.GetRequiredString(existingThread, "id", "Discord Bot Error", $"Discord returned an active thread without an id for name '{threadName}'.");
                Logger.Info($"Discord resolved active session thread '{existingThreadId}' directly from reconciliation snapshot for thread '{threadName}'");
                return new BotManagedThreadResolution(existingThreadId, reusableStarterMessage.CanEditFailureSeed);
            }

            if (reusableStarterMessage != null) {
                return await TryCreateThreadFromExistingStarterMessage(webhookMetadata, reusableStarterMessage, threadName);
            }

            Logger.Info($"Discord did not find a visible starter message for thread '{threadName}'. Creating a new starter message and thread.");
            var botCreatedThreadId = await CreateThreadWithBot(webhookMetadata, threadName);
            if (string.IsNullOrWhiteSpace(botCreatedThreadId)) {
                throw DiscordClient.CreateApiException("Discord Bot Error", "The Discord bot could not create a thread for this text channel.");
            }

            Logger.Info($"Discord created new session thread '{botCreatedThreadId}' for thread '{threadName}'");
            return new BotManagedThreadResolution(botCreatedThreadId, true);
        }

        internal async Task<JObject> FindActiveThreadByName(string guildId, string expectedParentChannelId, string threadName) {
            var response = await discordClient.SendBotRequest(HttpMethod.Get, $"https://discord.com/api/v10/guilds/{guildId}/threads/active");
            var threads = response["threads"] as JArray
                ?? throw DiscordClient.CreateApiException("Discord Bot Error", $"Discord did not return an active thread list for guild {guildId}.");

            Logger.Debug($"Discord active-thread lookup guildId='{guildId}' parentChannelId='{expectedParentChannelId}' threadName='{threadName}' activeThreadCount={threads.Count}");

            var matchingThreads = threads
                .OfType<JObject>()
                .Where(thread =>
                    string.Equals(thread["parent_id"]?.Value<string>(), expectedParentChannelId, StringComparison.Ordinal)
                    && string.Equals(thread["name"]?.Value<string>(), threadName, StringComparison.Ordinal))
                .OrderByDescending(thread => GetSnowflakeSortKey(thread["id"]?.Value<string>()))
                .ToList();

            if (matchingThreads.Count == 0) {
                Logger.Debug($"Discord active-thread lookup found no active thread named '{threadName}' for parentChannelId='{expectedParentChannelId}'");
                return null;
            }

            var matchingThread = matchingThreads[0];
            if (matchingThreads.Count > 1) {
                Logger.Warning($"Discord active-thread lookup found {matchingThreads.Count} active threads named '{threadName}' for parentChannelId='{expectedParentChannelId}'. Using newest thread '{matchingThread["id"]?.Value<string>()}'.");
            }

            ValidateThreadChannel(matchingThread, expectedParentChannelId, "Discord Bot Error", $"Discord active thread '{threadName}' is invalid.");
            return matchingThread;
        }

        internal async Task TryMarkThreadStarterMessageAsFailed(WebhookMetadata webhookMetadata, string threadId, string threadName, bool canEditFailureSeed) {
            if (!canEditFailureSeed) {
                Logger.Info($"Discord left starter message '{threadId}' unchanged after failure because the selected seed row for thread '{threadName}' is not owned by the configured webhook.");
                return;
            }

            var failedSeedText = DiscordSeed.BuildFailedSeedText(threadName);

            try {
                // A thread created from a message shares its id with that starter message, so the thread id addresses the starter message here.
                await discordClient.EditWebhookMessage(webhookMetadata.Url, threadId, new {
                    content = failedSeedText,
                    allowed_mentions = new {
                        parse = Array.Empty<string>(),
                    },
                }, notifyOnFailure: false);
                Logger.Info($"Discord marked starter message '{threadId}' as failed with content '{failedSeedText}'");
            } catch (Exception ex) {
                Logger.Error($"Failed to mark Discord starter message '{threadId}' as failed. {ex.Message}");
            }
        }

        private async Task<string> CreateThreadWithBot(WebhookMetadata webhookMetadata, string threadName) {
            var starterMessageId = await CreateStarterMessageWithWebhook(webhookMetadata, threadName);
            return await CreateThreadFromStarterMessageWithBot(webhookMetadata, starterMessageId, threadName);
        }

        private async Task<string> CreateThreadFromStarterMessageWithBot(WebhookMetadata webhookMetadata, string starterMessageId, string threadName) {
            Logger.Info($"Discord bot creating thread from starterMessageId='{starterMessageId}' parentChannelId='{webhookMetadata.ChannelId}' threadName='{threadName}'");
            var thread = await discordClient.SendBotRequest(HttpMethod.Post,
                $"https://discord.com/api/v10/channels/{webhookMetadata.ChannelId}/messages/{starterMessageId}/threads",
                new {
                    name = threadName,
                    auto_archive_duration = DefaultAutoArchiveDurationMinutes,
                });

            var threadId = DiscordClient.GetRequiredString(thread, "id", "Discord Bot Error", "Discord did not return a thread id after thread creation.");
            ValidateThreadChannel(thread, webhookMetadata.ChannelId, "Discord Bot Error", "Discord returned an invalid thread creation response.");

            var validatedThread = await discordClient.GetChannel(threadId);
            ValidateThreadChannel(validatedThread, webhookMetadata.ChannelId, "Discord Bot Error", "Discord could not validate the created thread.");
            await ValidateActiveThreadInGuild(webhookMetadata.GuildId, threadId, webhookMetadata.ChannelId, "Discord Bot Error", "Discord did not report the created thread as active.");
            Logger.Info($"Discord bot created threadId='{threadId}' for parentChannelId='{webhookMetadata.ChannelId}'");
            return threadId;
        }

        private async Task<string> CreateStarterMessageWithWebhook(WebhookMetadata webhookMetadata, string threadName) {
            var payload = new {
                content = DiscordSeed.BuildSeedText(threadName),
                username = DiscordSettings.Current.WebhookDefaultBotName,
                allowed_mentions = new {
                    parse = allowedMentionTypes,
                },
            };

            var message = await discordClient.PostWebhook(webhookMetadata.Url, payload, waitForResponse: true, notifyOnFailure: true);
            ValidateWebhookMessage(message, webhookMetadata.ChannelId, "Discord Webhook Error", "Discord returned an invalid webhook message response.");
            return DiscordClient.GetRequiredString(message, "id", "Discord Webhook Error", "Discord did not return a starter message id.");
        }

        private async Task<List<VisibleParentEntry>> PurgeUnattachedFailedSeedMessages(WebhookMetadata webhookMetadata, List<VisibleParentEntry> visibleEntries) {
            if (visibleEntries == null || visibleEntries.Count == 0) {
                return visibleEntries ?? [];
            }

            var unattachedFailedEntries = visibleEntries
                .Where(entry =>
                    string.IsNullOrWhiteSpace(entry.ThreadId)
                    && DiscordSeed.IsFailedSeedText(entry.SeedText))
                .OrderBy(entry => entry.Order)
                .ToList();

            if (unattachedFailedEntries.Count == 0) {
                return visibleEntries;
            }

            Logger.Warning($"Discord found {unattachedFailedEntries.Count} unattached failed seed message(s) in parent channel '{webhookMetadata.ChannelId}'. Deleting them before thread reconciliation.");
            await TryDeleteDuplicateParentMessages(webhookMetadata, unattachedFailedEntries.Select(entry => entry.MessageId), "unattached failed seed cleanup");

            var unattachedFailedMessageIds = unattachedFailedEntries
                .Select(entry => entry.MessageId)
                .ToHashSet(StringComparer.Ordinal);

            return visibleEntries
                .Where(entry => !unattachedFailedMessageIds.Contains(entry.MessageId))
                .ToList();
        }

        private async Task<BotManagedThreadResolution> TryCreateThreadFromExistingStarterMessage(WebhookMetadata webhookMetadata, ReusableStarterMessage reusableStarterMessage, string threadName) {
            if (reusableStarterMessage == null) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(reusableStarterMessage.ThreadId)) {
                var reusableThreadId = await TryReuseOrReopenThreadReferencedByParentMessage(webhookMetadata, reusableStarterMessage, threadName);
                if (!string.IsNullOrWhiteSpace(reusableThreadId)) {
                    if (reusableStarterMessage.DuplicateMessageIdsToDelete.Length > 0) {
                        await TryDeleteDuplicateParentMessages(webhookMetadata, reusableStarterMessage.DuplicateMessageIdsToDelete, threadName);
                    }
                    return new BotManagedThreadResolution(reusableThreadId, reusableStarterMessage.CanEditFailureSeed);
                }
            }

            Logger.Info($"Discord attempting to recreate thread '{threadName}' from existing parent message '{reusableStarterMessage.MessageId}'");
            var recreatedThreadId = await CreateThreadFromStarterMessageWithBot(webhookMetadata, reusableStarterMessage.MessageId, threadName);
            if (reusableStarterMessage.DuplicateMessageIdsToDelete.Length > 0) {
                await TryDeleteDuplicateParentMessages(webhookMetadata, reusableStarterMessage.DuplicateMessageIdsToDelete, threadName);
            }
            return new BotManagedThreadResolution(recreatedThreadId, reusableStarterMessage.CanEditFailureSeed);
        }

        private async Task<string> TryReuseOrReopenThreadReferencedByParentMessage(WebhookMetadata webhookMetadata, ReusableStarterMessage reusableStarterMessage, string threadName) {
            try {
                var existingThread = await discordClient.GetChannel(reusableStarterMessage.ThreadId, notifyOnFailure: false);
                ValidateThreadChannel(existingThread, webhookMetadata.ChannelId, "Discord Bot Error", $"Discord parent message '{reusableStarterMessage.MessageId}' pointed at an invalid thread.", notifyOnFailure: false);
                Logger.Info($"Discord reusing thread '{reusableStarterMessage.ThreadId}' from existing parent message '{reusableStarterMessage.MessageId}' for thread '{threadName}'");
                return reusableStarterMessage.ThreadId;
            } catch (Exception ex) {
                Logger.Warning($"Discord parent message '{reusableStarterMessage.MessageId}' referenced thread '{reusableStarterMessage.ThreadId}', but it could not be reused directly. Attempting to reopen it before recreation. {ex.Message}");
            }

            try {
                await discordClient.SendBotRequest(new HttpMethod("PATCH"),
                    $"https://discord.com/api/v10/channels/{reusableStarterMessage.ThreadId}",
                    new {
                        archived = false,
                        locked = false,
                    },
                    notifyOnFailure: false);

                var reopenedThread = await discordClient.GetChannel(reusableStarterMessage.ThreadId, notifyOnFailure: false);
                ValidateThreadChannel(reopenedThread, webhookMetadata.ChannelId, "Discord Bot Error", $"Discord could not reopen thread '{reusableStarterMessage.ThreadId}' referenced by parent message '{reusableStarterMessage.MessageId}'.", notifyOnFailure: false);
                Logger.Info($"Discord reopened thread '{reusableStarterMessage.ThreadId}' from existing parent message '{reusableStarterMessage.MessageId}' for thread '{threadName}'");
                return reusableStarterMessage.ThreadId;
            } catch (Exception ex) {
                Logger.Warning($"Discord could not reopen thread '{reusableStarterMessage.ThreadId}' referenced by parent message '{reusableStarterMessage.MessageId}'. Attempting recreation from the existing parent message. {ex.Message}");
                return null;
            }
        }

        private ReusableStarterMessage FindReusableStarterMessage(IEnumerable<VisibleParentEntry> visibleEntries, string parentChannelId, string threadName) {
            var matchingEntries = GetMatchingVisibleParentEntries(visibleEntries, threadName).ToList();
            if (matchingEntries.Count == 0) {
                return null;
            }

            var authoritativeEntry = GetAuthoritativeVisibleParentEntry(visibleEntries, threadName, parentChannelId);

            if (authoritativeEntry == null) {
                return null;
            }

            var duplicateMessageIdsToDelete = matchingEntries
                .Where(entry => !string.Equals(entry.MessageId, authoritativeEntry.MessageId, StringComparison.Ordinal))
                .OrderByDescending(entry => entry.Order)
                .Select(entry => entry.MessageId)
                .ToArray();

            Logger.Debug($"Discord found reusable starter message '{authoritativeEntry.MessageId}' for thread '{threadName}' with visibleThreadId='{authoritativeEntry.ThreadId ?? string.Empty}' seedText='{authoritativeEntry.SeedText}' duplicatesToDelete={duplicateMessageIdsToDelete.Length}");
            return new ReusableStarterMessage(authoritativeEntry.MessageId, authoritativeEntry.ThreadId, authoritativeEntry.IsWebhookOwned, duplicateMessageIdsToDelete);
        }

        private async Task<JObject> ResolveActiveThreadFromSnapshot(IEnumerable<JObject> candidateThreads, IEnumerable<VisibleParentEntry> visibleEntries, WebhookMetadata webhookMetadata, string targetThreadName) {
            var candidateThreadList = candidateThreads?.ToList() ?? new List<JObject>();
            var visibleEntryList = visibleEntries?.ToList() ?? new List<VisibleParentEntry>();
            var parentChannelId = webhookMetadata.ChannelId;

            if (candidateThreadList.Count == 0) {
                return null;
            }

            JObject resolvedThread = null;
            var resolvedThreadOrder = int.MaxValue;

            foreach (var thread in candidateThreadList) {
                var threadId = thread["id"]?.Value<string>();
                var threadName = thread["name"]?.Value<string>();

                if (string.IsNullOrWhiteSpace(threadId) || string.IsNullOrWhiteSpace(threadName)) {
                    continue;
                }

                // Only reconcile the session thread being sent right now.
                // A send for a new day must not delete yesterday's still-active thread.
                if (!string.Equals(threadName, targetThreadName, StringComparison.Ordinal)) {
                    continue;
                }

                var authoritativeEntry = GetAuthoritativeVisibleParentEntry(visibleEntryList, threadName, parentChannelId);

                if (authoritativeEntry == null) {
                    await TryDeleteOwnedThread(thread, visibleEntryList,
                        $"no seed message created by Ground Station exists for seed text '{threadName}' inside the non-archived scan window");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(authoritativeEntry.ThreadId)) {
                    await TryDeleteOwnedThread(thread, visibleEntryList,
                        $"seed message '{authoritativeEntry.MessageId}' with seed text '{authoritativeEntry.SeedText}' has no attached thread and should be reused");
                    continue;
                }

                if (!string.Equals(authoritativeEntry.ThreadId, threadId, StringComparison.Ordinal)) {
                    await TryDeleteOwnedThread(thread, visibleEntryList,
                        $"seed message '{authoritativeEntry.MessageId}' points to thread '{authoritativeEntry.ThreadId}' for seed text '{authoritativeEntry.SeedText}'");
                    continue;
                }

                var isArchived = thread["thread_metadata"]?["archived"]?.Value<bool>() ?? true;
                if (isArchived) {
                    continue;
                }

                if (resolvedThread == null || authoritativeEntry.Order < resolvedThreadOrder) {
                    if (resolvedThread != null) {
                        Logger.Warning($"Discord found multiple visible active threads named '{targetThreadName}' for parent channel '{parentChannelId}'. Using newer authoritative visible thread '{threadId}' instead of '{resolvedThread["id"]?.Value<string>()}'.");
                    }
                    resolvedThread = thread;
                    resolvedThreadOrder = authoritativeEntry.Order;
                } else {
                    Logger.Warning($"Discord found multiple visible active threads named '{targetThreadName}' for parent channel '{parentChannelId}'. Keeping newer authoritative visible thread '{resolvedThread["id"]?.Value<string>()}' and ignoring '{threadId}'.");
                }
            }

            return resolvedThread;
        }

        private async Task<List<JObject>> GetOrphanCleanupCandidates(string guildId, string parentChannelId) {
            var candidates = new Dictionary<string, JObject>(StringComparer.Ordinal);

            var activeResponse = await discordClient.SendBotRequest(HttpMethod.Get, $"https://discord.com/api/v10/guilds/{guildId}/threads/active", notifyOnFailure: true);
            AddOrphanCleanupCandidates(candidates, activeResponse["threads"] as JArray, parentChannelId);

            Logger.Debug($"Discord seed cleanup parentChannelId='{parentChannelId}' activeCandidateThreadCount={candidates.Count}");
            return candidates.Values.ToList();
        }

        private void AddOrphanCleanupCandidates(IDictionary<string, JObject> candidates, JArray threads, string expectedParentChannelId) {
            if (threads == null) {
                return;
            }

            foreach (var thread in threads.OfType<JObject>()) {
                var threadId = thread["id"]?.Value<string>();
                var parentId = thread["parent_id"]?.Value<string>();
                var channelType = thread["type"]?.Value<int>();

                if (string.IsNullOrWhiteSpace(threadId) || !channelType.HasValue) {
                    continue;
                }

                if (!string.Equals(parentId, expectedParentChannelId, StringComparison.Ordinal)) {
                    continue;
                }

                if (!IsMessageBasedThreadChannelType(channelType.Value)) {
                    continue;
                }

                candidates[threadId] = thread;
            }
        }

        private async Task<List<VisibleParentEntry>> GetVisibleParentEntries(WebhookMetadata webhookMetadata) {
            var visibleEntries = new List<VisibleParentEntry>();
            var scanOrder = 0;
            var cutoffUtc = DateTimeOffset.UtcNow - VisibleParentScanWindow;
            var parentChannelId = webhookMetadata.ChannelId;

            await foreach (var message in discordClient.EnumerateChannelMessages(parentChannelId, cutoffUtc, CancellationToken.None)) {
                var messageCreatedAt = DiscordClient.GetMessageCreatedAt(message);
                var messageId = message["id"]?.Value<string>();
                var messageWebhookId = message["webhook_id"]?.Value<string>()?.Trim();
                var messageContent = message["content"]?.Value<string>()?.Trim();
                var threadId = message["thread"]?["id"]?.Value<string>()?.Trim();
                var threadName = message["thread"]?["name"]?.Value<string>()?.Trim();

                if (DiscordSeed.IsDirectChannelPost(messageWebhookId, webhookMetadata.WebhookId, messageContent)) {
                    continue;
                }

                var hasSeedMarker = DiscordSeed.HasSeedMarker(messageContent);
                var isWebhookOwned = DiscordSeed.IsOwnedWebhook(messageWebhookId, webhookMetadata.WebhookId);

                var seedText = DiscordSeed.NormalizeSeedText(messageContent);

                Logger.Debug($"Discord parent message scan channelId='{parentChannelId}' messageId='{messageId}' createdAtUtc='{messageCreatedAt?.ToString("O") ?? string.Empty}' threadId='{threadId ?? string.Empty}' threadName='{threadName ?? string.Empty}' hasSeedMarker={hasSeedMarker} isWebhookOwned={isWebhookOwned}");

                if (!string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(seedText) && hasSeedMarker) {
                    visibleEntries.Add(new VisibleParentEntry(messageId, seedText, threadId, scanOrder, isWebhookOwned));
                }

                scanOrder++;
            }

            Logger.Debug($"Discord seed cleanup parentChannelId='{parentChannelId}' visibleParentEntryCount={visibleEntries.Count}");
            return visibleEntries;
        }

        private VisibleParentEntry GetAuthoritativeVisibleParentEntry(IEnumerable<VisibleParentEntry> visibleEntries, string threadName, string parentChannelId) {
            var matches = GetMatchingVisibleParentEntries(visibleEntries, threadName).ToList();

            if (matches.Count == 0) {
                return null;
            }

            var threadedMatches = matches
                .Where(entry => !string.IsNullOrWhiteSpace(entry.ThreadId))
                .OrderBy(entry => entry.Order)
                .ToList();
            var authoritativeEntry = threadedMatches.FirstOrDefault()
                ?? matches.OrderBy(entry => entry.Order).First();

            if (matches.Count > 1) {
                var selectionMode = threadedMatches.Count > 0
                    ? "newest message with an attached thread"
                    : "newest message because none of the duplicates have an attached thread";
                Logger.Warning($"Discord found multiple visible parent messages for seed text '{threadName}' in parent channel '{parentChannelId}'. Using {selectionMode} '{authoritativeEntry.MessageId}'.");
            }

            return authoritativeEntry;
        }

        private IEnumerable<VisibleParentEntry> GetMatchingVisibleParentEntries(IEnumerable<VisibleParentEntry> visibleEntries, string threadName) {
            return visibleEntries.Where(entry => DiscordSeed.SeedTextMatchesThreadName(entry.SeedText, threadName));
        }

        private async Task TryDeleteOwnedThread(JObject thread, IEnumerable<VisibleParentEntry> visibleEntries, string reason) {
            var threadId = thread["id"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(threadId)) {
                return;
            }

            if (!await IsOwnedSessionThread(thread, threadId, visibleEntries)) {
                Logger.Warning($"Discord left thread '{threadId}' in place even though {reason}, because it was not created by Ground Station.");
                return;
            }

            Logger.Info($"Discord deleting session thread '{threadId}' because {reason}.");

            try {
                await discordClient.SendBotRequest(HttpMethod.Delete, $"https://discord.com/api/v10/channels/{threadId}", notifyOnFailure: false);
            } catch (DiscordApiException ex) when (ex.Message.Contains("returned 404", StringComparison.Ordinal)) {
                Logger.Info($"Discord cleanup delete for thread '{threadId}' returned 404 because the thread was already gone. Treating it as successfully removed.");
            } catch (DiscordApiException ex) {
                Logger.Error($"Failed to delete stale session thread '{threadId}'. Continuing without cleanup. {ex.Message}");
            }
        }

        private async Task<bool> IsOwnedSessionThread(JObject thread, string threadId, IEnumerable<VisibleParentEntry> visibleEntries) {
            if (visibleEntries.Any(entry =>
                    string.Equals(entry.MessageId, threadId, StringComparison.Ordinal)
                    || string.Equals(entry.ThreadId, threadId, StringComparison.Ordinal))) {
                return true;
            }

            var botUserId = await discordClient.TryGetBotUserId();
            if (string.IsNullOrWhiteSpace(botUserId)) {
                return false;
            }

            return string.Equals(thread["owner_id"]?.Value<string>(), botUserId, StringComparison.Ordinal);
        }

        private async Task TryDeleteDuplicateParentMessages(WebhookMetadata webhookMetadata, IEnumerable<string> duplicateMessageIds, string threadName) {
            foreach (var messageId in duplicateMessageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)) {
                try {
                    await discordClient.DeleteChannelMessage(webhookMetadata.ChannelId, messageId, notifyOnFailure: false);
                    Logger.Info($"Discord deleted older duplicate seed message '{messageId}' for seed text '{threadName}'");
                } catch (DiscordApiException ex) {
                    Logger.Error($"Failed to delete older duplicate seed message '{messageId}' for seed text '{threadName}'. {ex.Message}");
                }
            }
        }

        private static ulong GetSnowflakeSortKey(string snowflake) {
            return ulong.TryParse(snowflake, out var value) ? value : 0;
        }

        private static bool IsThreadChannelType(int channelType) {
            return channelType == AnnouncementThreadChannelType
                || channelType == PublicThreadChannelType
                || channelType == PrivateThreadChannelType;
        }

        private static bool IsMessageBasedThreadChannelType(int channelType) {
            return channelType == AnnouncementThreadChannelType
                || channelType == PublicThreadChannelType;
        }
    }
}
