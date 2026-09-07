using DaleGhent.NINA.GroundStation.DiscordWebhook;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    [Collection("Discord")]
    public sealed class DiscordSessionThreadsTests : IDisposable {
        private readonly DiscordTestContext context = new();

        public void Dispose() => context.Dispose();

        [Fact]
        public void CanFallBackToPlainWebhook_DetectsForumOnlyError() {
            Assert.True(DiscordSessionThreads.CanFallBackToPlainWebhook(new DiscordApiException("{\"code\": 220003}")));
            Assert.True(DiscordSessionThreads.CanFallBackToPlainWebhook(new DiscordApiException("Webhooks can only create threads in forum channels")));
            Assert.False(DiscordSessionThreads.CanFallBackToPlainWebhook(new DiscordApiException("returned 500")));
        }

        [Fact]
        public void ValidateWebhookMessage_RejectsWrongChannel() {
            var message = new JObject { ["id"] = "1", ["channel_id"] = "other" };
            var ex = Assert.Throws<DiscordApiException>(() =>
                context.SessionThreads.ValidateWebhookMessage(message, FakeDiscordApi.ChannelId, "Discord Webhook Error", "bad."));
            Assert.Contains(FakeDiscordApi.ChannelId, ex.Message);
            Assert.Contains("other", ex.Message);
        }

        [Fact]
        public void ValidateParentChannel_RejectsIdMismatch() {
            var channel = new JObject { ["id"] = "nope", ["type"] = 0 };
            Assert.Throws<DiscordApiException>(() =>
                context.SessionThreads.ValidateParentChannel(channel, FakeDiscordApi.ChannelId));
        }

        [Fact]
        public void ValidateThreadChannel_RejectsArchivedLockedWrongParentAndNonThread() {
            var archived = ThreadJson("t1", archived: true);
            var locked = ThreadJson("t2", locked: true);
            var wrongParent = ThreadJson("t3", parentId: "elsewhere");
            var notThread = new JObject { ["id"] = "t4", ["type"] = 0, ["parent_id"] = FakeDiscordApi.ChannelId, ["thread_metadata"] = new JObject { ["archived"] = false, ["locked"] = false } };

            Assert.Throws<DiscordApiException>(() => context.SessionThreads.ValidateThreadChannel(archived, FakeDiscordApi.ChannelId, "Discord Bot Error", "bad."));
            Assert.Throws<DiscordApiException>(() => context.SessionThreads.ValidateThreadChannel(locked, FakeDiscordApi.ChannelId, "Discord Bot Error", "bad."));
            Assert.Throws<DiscordApiException>(() => context.SessionThreads.ValidateThreadChannel(wrongParent, FakeDiscordApi.ChannelId, "Discord Bot Error", "bad."));
            Assert.Throws<DiscordApiException>(() => context.SessionThreads.ValidateThreadChannel(notThread, FakeDiscordApi.ChannelId, "Discord Bot Error", "bad."));
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_CreatesStarterAndThreadWhenNoneExist() {
            var resolved = await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.False(string.IsNullOrWhiteSpace(resolved.ThreadId));
            Assert.True(resolved.CanEditFailureSeed);
            Assert.Contains(context.Api.Channels.Values, channel => channel.Name == "test-session" && channel.Type == 11);
            Assert.Contains(context.Api.Messages, message => DiscordSeed.HasSeedMarker(message.Content));
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_ReusesExistingVisibleThread() {
            var seed = DiscordSeed.BuildSeedText("test-session");
            var starter = context.Api.AddParentMessage(seed, DateTimeOffset.UtcNow.AddMinutes(-10), threadId: "thread-existing", threadName: "test-session", messageId: "thread-existing");
            context.Api.AddThread(starter.Id, "test-session");

            var resolved = await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.Equal("thread-existing", resolved.ThreadId);
            Assert.DoesNotContain(context.Api.Requests, request => request.Url.EndsWith("/threads", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_ReopensArchivedThread() {
            var seed = DiscordSeed.BuildSeedText("test-session");
            var starter = context.Api.AddParentMessage(seed, DateTimeOffset.UtcNow.AddMinutes(-10), threadId: "thread-archived", threadName: "test-session", messageId: "thread-archived");
            context.Api.AddThread(starter.Id, "test-session", archived: true);

            var resolved = await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.Equal("thread-archived", resolved.ThreadId);
            Assert.False(context.Api.Channels["thread-archived"].Archived);
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_DeletesOwnedDuplicateNotLastNightsThread() {
            var tonight = DiscordSeed.BuildSeedText("test-session");
            var lastNight = DiscordSeed.BuildSeedText("yesterday");
            var tonightStarter = context.Api.AddParentMessage(tonight, DateTimeOffset.UtcNow.AddMinutes(-5), threadId: "tonight", threadName: "test-session", messageId: "tonight");
            var duplicate = context.Api.AddParentMessage(tonight, DateTimeOffset.UtcNow.AddMinutes(-20), threadId: "dup", threadName: "test-session", messageId: "dup");
            var yesterday = context.Api.AddParentMessage(lastNight, DateTimeOffset.UtcNow.AddHours(-8), threadId: "yesterday", threadName: "yesterday", messageId: "yesterday");
            context.Api.AddThread(tonightStarter.Id, "test-session");
            context.Api.AddThread(duplicate.Id, "test-session");
            context.Api.AddThread(yesterday.Id, "yesterday");

            var resolved = await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.Equal("tonight", resolved.ThreadId);
            Assert.False(context.Api.Channels.ContainsKey("dup"));
            Assert.True(context.Api.Channels.ContainsKey("yesterday"));
            Assert.DoesNotContain(context.Api.DeletedUrls, url => url.Contains("/messages/yesterday", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_LeavesHumanOwnedOrphanInPlace() {
            context.Api.AddThread("human-thread", "test-session", ownerId: "someone-else");

            var resolved = await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.True(context.Api.Channels.ContainsKey("human-thread"));
            Assert.NotEqual("human-thread", resolved.ThreadId);
        }

        [Fact]
        public async Task ResolveBotManagedTextThread_PurgesUnattachedFailedSeeds() {
            var failed = DiscordSeed.BuildFailedSeedText("test-session");
            context.Api.AddParentMessage(failed, DateTimeOffset.UtcNow.AddMinutes(-2), messageId: "failed-loose");

            await context.SessionThreads.ResolveBotManagedTextThread(Metadata(), "test-session");

            Assert.DoesNotContain(context.Api.Messages, message => message.Id == "failed-loose");
        }

        [Fact]
        public async Task TryMarkThreadStarterMessageAsFailed_EditsWebhookOwnedSeedOnly() {
            var seed = DiscordSeed.BuildSeedText("test-session");
            context.Api.AddParentMessage(seed, DateTimeOffset.UtcNow, threadId: "thread-fail", threadName: "test-session", messageId: "thread-fail");

            await context.SessionThreads.TryMarkThreadStarterMessageAsFailed(Metadata(), "thread-fail", "test-session", canEditFailureSeed: true);
            await context.SessionThreads.TryMarkThreadStarterMessageAsFailed(Metadata(), "thread-fail", "test-session", canEditFailureSeed: false);

            var message = Assert.Single(context.Api.Messages, item => item.Id == "thread-fail");
            Assert.Equal(DiscordSeed.BuildFailedSeedText("test-session"), message.Content);
        }

        [Fact]
        public async Task FindActiveThreadByName_UsesNewestWhenDuplicatesExist() {
            context.Api.AddThread("older", "forum-night", type: 11);
            context.Api.AddThread("newer", "forum-night", type: 11);
            context.Api.Channels["older"].Id = "100";
            context.Api.Channels["newer"].Id = "200";
            context.Api.Channels.Remove("older");
            context.Api.Channels.Remove("newer");
            context.Api.AddThread("100", "forum-night");
            context.Api.AddThread("200", "forum-night");

            var found = await context.SessionThreads.FindActiveThreadByName(FakeDiscordApi.GuildId, FakeDiscordApi.ChannelId, "forum-night");

            Assert.Equal("200", found.Value<string>("id"));
        }

        private static WebhookMetadata Metadata() {
            return new WebhookMetadata(FakeDiscordApi.WebhookUrl, FakeDiscordApi.WebhookId, FakeDiscordApi.ChannelId, FakeDiscordApi.GuildId);
        }

        private static JObject ThreadJson(string id, bool archived = false, bool locked = false, string parentId = FakeDiscordApi.ChannelId) {
            return new JObject {
                ["id"] = id,
                ["type"] = 11,
                ["parent_id"] = parentId,
                ["thread_metadata"] = new JObject {
                    ["archived"] = archived,
                    ["locked"] = locked,
                },
            };
        }
    }
}
