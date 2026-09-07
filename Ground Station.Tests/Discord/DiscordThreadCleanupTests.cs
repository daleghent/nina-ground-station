using DaleGhent.NINA.GroundStation.DiscordWebhook;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    [Collection("Discord")]
    public sealed class DiscordThreadCleanupTests : IDisposable {
        private readonly DiscordTestContext context = new();

        public void Dispose() => context.Dispose();

        [Fact]
        public async Task DeleteOldSessionThreads_RequiresWebhookAndBotToken() {
            context.Settings.WebhookDefaultUrl = string.Empty;
            await Assert.ThrowsAsync<DiscordApiException>(() => context.Cleanup.DeleteOldSessionThreads(1));

            context.Settings.WebhookDefaultUrl = FakeDiscordApi.WebhookUrl;
            context.Settings.BotToken = " ";
            await Assert.ThrowsAsync<DiscordApiException>(() => context.Cleanup.DeleteOldSessionThreads(1));
        }

        [Fact]
        public async Task DeleteOldSessionThreads_RejectsForumChannels() {
            context.Api.ParentChannelType = 15;
            var ex = await Assert.ThrowsAsync<DiscordApiException>(() => context.Cleanup.DeleteOldSessionThreads(1));
            Assert.Contains("normal text or announcement", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DeleteOldSessionThreads_SkipsCurrentSessionHumanPostsAndYoungSeeds() {
            var now = DateTimeOffset.Now;
            var old = now.AddDays(-40);
            var tonight = DiscordSeed.BuildSeedText("test-session");
            var oldSeed = DiscordSeed.BuildSeedText("old-night");
            var human = "End of night summary";

            var current = context.Api.AddParentMessage(tonight, old, threadId: "current", threadName: "test-session", messageId: "current");
            var stale = context.Api.AddParentMessage(oldSeed, old, threadId: "stale", threadName: "old-night", messageId: "stale");
            var young = context.Api.AddParentMessage(oldSeed, now.AddHours(-1), threadId: "young", threadName: "old-night", messageId: "young");
            var channelPost = context.Api.AddParentMessage(human, old, threadId: "human-thread", threadName: "old-night", messageId: "human", webhookId: "someone-else");
            context.Api.AddThread(current.Id, "test-session");
            context.Api.AddThread(stale.Id, "old-night");
            context.Api.AddThread(young.Id, "old-night");
            context.Api.AddThread("human-thread", "old-night", ownerId: "human");

            var deleted = await context.Cleanup.DeleteOldSessionThreads(30);

            Assert.Equal(1, deleted);
            Assert.False(context.Api.Channels.ContainsKey("stale"));
            Assert.True(context.Api.Channels.ContainsKey("current"));
            Assert.True(context.Api.Channels.ContainsKey("young"));
            Assert.True(context.Api.Channels.ContainsKey("human-thread"));
            Assert.DoesNotContain(context.Api.Messages, message => message.Id == "stale");
            Assert.Contains(context.Api.Messages, message => message.Id == "human");
        }

        [Fact]
        public async Task DeleteOldSessionThreads_NeverDeletesPluginDirectChannelPosts() {
            var old = DateTimeOffset.Now.AddDays(-40);
            var productionFinished = "09/06/2026 - 01:53:58  -  IMAGING:   Completely Finished Undefined: Galaxy Superman Galaxy / NGC 7479";
            var otherChannelPost = "09/06/2026 - 22:10:00  -  IMAGING:   Meridian Flip";
            var embedOnlyChannelPost = new[] {
                new {
                    title = "Session summary",
                    fields = new[] {
                        new { name = "Target", value = "IC 1396 / Elephant Trunk" }
                    }
                }
            };

            context.Api.AddParentMessage(DiscordSeed.BuildSeedText("old-night"), old, threadId: "stale", threadName: "old-night", messageId: "stale");
            context.Api.AddParentMessage(productionFinished, old, threadId: "finished-thread", threadName: "old-night", messageId: "finished");
            context.Api.AddParentMessage(otherChannelPost, old, threadId: "flip-thread", threadName: "old-night", messageId: "flip");
            context.Api.AddParentMessage(string.Empty, old, threadId: "embed-thread", threadName: "old-night", messageId: "embed", embeds: embedOnlyChannelPost);
            context.Api.AddThread("stale", "old-night");
            context.Api.AddThread("finished-thread", "old-night");
            context.Api.AddThread("flip-thread", "old-night");
            context.Api.AddThread("embed-thread", "old-night");

            var deleted = await context.Cleanup.DeleteOldSessionThreads(30);

            Assert.Equal(1, deleted);
            Assert.False(context.Api.Channels.ContainsKey("stale"));
            Assert.DoesNotContain(context.Api.Messages, message => message.Id == "stale");
            Assert.Contains(context.Api.Messages, message => message.Id == "finished");
            Assert.Contains(context.Api.Messages, message => message.Id == "flip");
            Assert.Contains(context.Api.Messages, message => message.Id == "embed");
            Assert.True(context.Api.Channels.ContainsKey("finished-thread"));
            Assert.True(context.Api.Channels.ContainsKey("flip-thread"));
            Assert.True(context.Api.Channels.ContainsKey("embed-thread"));
        }

        [Fact]
        public async Task DeleteOldSessionThreads_FailedHistoryDoesNotDelete() {
            context.Api.MessageListFailuresRemaining = 1;
            context.Api.AddParentMessage(DiscordSeed.BuildSeedText("old-night"), DateTimeOffset.Now.AddDays(-40), threadId: "stale", threadName: "old-night", messageId: "stale");
            context.Api.AddThread("stale", "old-night");

            await Assert.ThrowsAsync<DiscordApiException>(() => context.Cleanup.DeleteOldSessionThreads(1));

            Assert.True(context.Api.Channels.ContainsKey("stale"));
            Assert.Equal(0, context.Api.DeleteRequestCount);
        }

        [Fact]
        public async Task DeleteOldSessionThreads_ReportsProgressAndHonorsCancel() {
            var old = DateTimeOffset.Now.AddDays(-40);
            context.Api.AddParentMessage(DiscordSeed.BuildSeedText("old-night"), old, threadId: "stale", threadName: "old-night", messageId: "stale");
            context.Api.AddThread("stale", "old-night");

            var reports = new List<ThreadCleanupProgress>();
            var progress = new Progress<ThreadCleanupProgress>(reports.Add);
            var deleted = await context.Cleanup.DeleteOldSessionThreads(30, progress);
            Assert.Equal(1, deleted);
            Assert.Contains(reports, report => report.LogLine.Contains("Scanning", StringComparison.OrdinalIgnoreCase) || report.CompletedCount > 0);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Cleanup.DeleteOldSessionThreads(30, cancellationToken: cts.Token));
        }
    }
}
