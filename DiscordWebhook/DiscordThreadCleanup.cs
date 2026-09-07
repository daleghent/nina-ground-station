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
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GSUtilities = DaleGhent.NINA.GroundStation.Utilities.Utilities;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    internal sealed class DiscordThreadCleanup {
        private readonly DiscordClient discordClient;

        internal DiscordThreadCleanup() : this(new DiscordClient()) {
        }

        internal DiscordThreadCleanup(DiscordClient discordClient) {
            this.discordClient = discordClient;
        }

        /// <summary>
        /// Deletes session threads that Ground Station created at least <paramref name="minimumAgeDays"/> days ago,
        /// along with the top-level starter messages that anchor them and every message inside them. Only messages
        /// carrying Ground Station's invisible session-seed marker are eligible: messages this plugin posted directly to
        /// the parent channel (Send to Discord with "Post directly to channel", including end-of-night summaries) are
        /// always kept, even if a thread was later attached to them, as is anything a person or another integration posted.
        /// The current session's thread is never deleted, even when it is old enough by the age cutoff.
        /// </summary>
        /// <returns>The number of session threads deleted.</returns>
        public async Task<int> DeleteOldSessionThreads(int minimumAgeDays, IProgress<ThreadCleanupProgress> progress = null, CancellationToken cancellationToken = default) {
            if (string.IsNullOrEmpty(DiscordSettings.Current.WebhookDefaultUrl)) {
                throw DiscordClient.CreateApiException("Discord Webhook Error", "No webhook URL is set.");
            }

            if (string.IsNullOrWhiteSpace(DiscordSettings.Current.BotToken)) {
                throw DiscordClient.CreateApiException("Discord Bot Error", "Deleting old session threads requires a bot token.");
            }

            var webhookMetadata = await discordClient.GetWebhookMetadata(DiscordSettings.Current.WebhookDefaultUrl);
            var parentChannel = await discordClient.GetChannel(webhookMetadata.ChannelId);
            if (!DiscordClient.RequiresBotThreadCreation(parentChannel)) {
                throw DiscordClient.CreateApiException("Discord Bot Error", "Old-thread cleanup is only available when the webhook points to a normal text or announcement channel.");
            }

            // Age is measured from local midnight so "1 day" spares everything posted today. The cutoff
            // is additionally capped at the current session's start so a session that began yesterday
            // evening and is still running can never lose its own thread. Candidates whose Discord
            // thread name matches tonight's session key are skipped as a second guard.
            var now = DateTime.Now;
            var rollover = DiscordSettings.Current.SessionRolloverTimeSpan;
            var currentSessionStart = GSUtilities.SessionDateTime(now, rollover).Date + rollover;
            var cutoffDateTime = now.Date.AddDays(-minimumAgeDays);
            if (cutoffDateTime > currentSessionStart) {
                cutoffDateTime = currentSessionStart;
            }
            var cutoff = new DateTimeOffset(cutoffDateTime);
            var currentThreadKey = GSUtilities.ResolveTokens(DiscordSettings.Current.ThreadNameTemplate, nowOverride: now).Trim();
            Logger.Info($"Discord old-thread cleanup starting parentChannelId='{webhookMetadata.ChannelId}' minimumAgeDays={minimumAgeDays} cutoff='{cutoff:O}' currentThreadKey='{currentThreadKey}'");

            var candidates = await FindOldSessionThreads(webhookMetadata, cutoff, currentThreadKey, progress, cancellationToken);

            if (candidates.Count == 0) {
                progress?.Report(new ThreadCleanupProgress("No session threads are old enough to delete.", 0, 0, TimeSpan.Zero));
                Logger.Info($"Discord old-thread cleanup found nothing to delete in parentChannelId='{webhookMetadata.ChannelId}'");
                return 0;
            }

            progress?.Report(new ThreadCleanupProgress($"Found {candidates.Count} session thread(s) to delete.", 0, candidates.Count, null));

            var deletedThreadCount = 0;
            var attemptedCount = 0;
            var stopwatch = Stopwatch.StartNew();

            foreach (var candidate in candidates) {
                cancellationToken.ThrowIfCancellationRequested();
                attemptedCount++;
                string logLine;

                try {
                    Logger.Info($"Discord old-thread cleanup deleting thread '{candidate.ThreadId}' and starter message '{candidate.MessageId}' created '{candidate.CreatedAt:O}'");

                    try {
                        await discordClient.SendBotRequest(HttpMethod.Delete, $"https://discord.com/api/v10/channels/{candidate.ThreadId}", notifyOnFailure: false);
                    } catch (DiscordApiException ex) when (ex.Message.Contains("returned 404", StringComparison.Ordinal)) {
                        Logger.Info($"Discord old-thread cleanup found thread '{candidate.ThreadId}' already gone.");
                    }

                    await discordClient.DeleteChannelMessage(webhookMetadata.ChannelId, candidate.MessageId, notifyOnFailure: false);
                    deletedThreadCount++;
                    logLine = $"Deleted '{candidate.DisplayName}' from {candidate.CreatedAt.ToLocalTime():yyyy-MM-dd}";
                } catch (DiscordApiException ex) {
                    Logger.Error($"Discord old-thread cleanup could not delete thread '{candidate.ThreadId}' or its starter message '{candidate.MessageId}'. Continuing. {ex.Message}");
                    logLine = $"FAILED to delete '{candidate.DisplayName}' — see the NINA log for details";
                }

                var remainingCount = candidates.Count - attemptedCount;
                var estimatedTimeRemaining = remainingCount > 0
                    ? TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / attemptedCount * remainingCount)
                    : TimeSpan.Zero;
                progress?.Report(new ThreadCleanupProgress(logLine, attemptedCount, candidates.Count, estimatedTimeRemaining));
            }

            progress?.Report(new ThreadCleanupProgress($"Finished. Deleted {deletedThreadCount} of {candidates.Count} session thread(s).", candidates.Count, candidates.Count, TimeSpan.Zero));
            Logger.Info($"Discord old-thread cleanup finished parentChannelId='{webhookMetadata.ChannelId}' deletedThreadCount={deletedThreadCount}");
            return deletedThreadCount;
        }

        private async Task<List<OldSessionThreadCandidate>> FindOldSessionThreads(WebhookMetadata webhookMetadata, DateTimeOffset cutoff, string currentThreadKey, IProgress<ThreadCleanupProgress> progress, CancellationToken cancellationToken) {
            var candidates = new List<OldSessionThreadCandidate>();
            var scannedMessageCount = 0;

            progress?.Report(new ThreadCleanupProgress($"Scanning channel history for session threads created before {cutoff.LocalDateTime:g}...", 0, 0, null));

            await foreach (var message in discordClient.EnumerateChannelMessages(webhookMetadata.ChannelId, untilUtc: null, cancellationToken)) {
                scannedMessageCount++;

                var messageId = message["id"]?.Value<string>();
                var createdAt = DiscordClient.GetMessageCreatedAt(message);
                if (createdAt.HasValue && createdAt.Value < cutoff) {
                    var threadId = message["thread"]?["id"]?.Value<string>()?.Trim();
                    var messageContent = message["content"]?.Value<string>()?.Trim();
                    var messageWebhookId = message["webhook_id"]?.Value<string>()?.Trim();

                    // Anything this webhook posted to the parent channel that is not a session-thread
                    // seed stays put — including bypass/"Post directly to channel" traffic, even if a
                    // thread was later attached to the message.
                    if (DiscordSeed.IsDirectChannelPost(messageWebhookId, webhookMetadata.WebhookId, messageContent)) {
                        Logger.Info($"Discord old-thread cleanup skipped Ground Station channel message '{messageId}'");
                        continue;
                    }

                    // Only Ground Station session-thread seed messages are eligible. Requiring both the
                    // invisible marker and our webhook id leaves human and other-bot posts alone.
                    if (!string.IsNullOrWhiteSpace(threadId)
                        && DiscordSeed.HasSeedMarker(messageContent)
                        && DiscordSeed.IsOwnedWebhook(messageWebhookId, webhookMetadata.WebhookId)) {
                        var threadName = message["thread"]?["name"]?.Value<string>()?.Trim();
                        if (!string.IsNullOrWhiteSpace(currentThreadKey)
                            && string.Equals(threadName, currentThreadKey, StringComparison.Ordinal)) {
                            Logger.Info($"Discord old-thread cleanup skipped current session thread '{threadId}' named '{threadName}'");
                        } else {
                            var displayName = string.IsNullOrWhiteSpace(threadName) ? $"thread {threadId}" : threadName;
                            candidates.Add(new OldSessionThreadCandidate(threadId, messageId, displayName, createdAt.Value));
                        }
                    }
                }

                if (scannedMessageCount % 100 == 0) {
                    progress?.Report(new ThreadCleanupProgress($"Scanned {scannedMessageCount} channel message(s), found {candidates.Count} old session thread(s) so far...", 0, 0, null));
                }
            }

            progress?.Report(new ThreadCleanupProgress($"Scanned {scannedMessageCount} channel message(s), found {candidates.Count} old session thread(s) so far...", 0, 0, null));
            return candidates;
        }
    }
}
