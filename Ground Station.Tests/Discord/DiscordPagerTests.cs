using DaleGhent.NINA.GroundStation.DiscordWebhook;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    [Collection("Discord")]
    public sealed class DiscordPagerTests : IDisposable {
        private readonly DiscordTestContext context = new();

        public void Dispose() => context.Dispose();

        [Fact]
        public async Task EnumerateChannelMessages_PagesPastOneHundredUntilCutoff() {
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < 150; i++) {
                context.Api.AddParentMessage($"msg-{i}", now.AddMinutes(-i), messageId: (1000000UL - (ulong)i).ToString());
            }

            for (var i = 0; i < 20; i++) {
                context.Api.AddParentMessage($"old-{i}", now.AddHours(-30).AddMinutes(-i), messageId: (100UL - (ulong)i).ToString());
            }

            var yielded = new List<JObject>();
            await foreach (var message in context.Client.EnumerateChannelMessages(FakeDiscordApi.ChannelId, now.AddHours(-24))) {
                yielded.Add(message);
            }

            Assert.Equal(150, yielded.Count);
            Assert.True(context.Api.MessageListRequestCount >= 2);
            Assert.DoesNotContain(yielded, message => message.Value<string>("content")?.StartsWith("old-", StringComparison.Ordinal) == true);
        }

        [Fact]
        public async Task EnumerateChannelMessages_DoesNotYieldTheCutoffMessage() {
            var now = DateTimeOffset.UtcNow;
            var cutoff = now.AddHours(-24);
            context.Api.AddParentMessage("new", now.AddHours(-1), messageId: "300");
            context.Api.AddParentMessage("on-cutoff", cutoff, messageId: "200");
            context.Api.AddParentMessage("old", cutoff.AddMinutes(-1), messageId: "100");

            var yielded = new List<string>();
            await foreach (var message in context.Client.EnumerateChannelMessages(FakeDiscordApi.ChannelId, cutoff)) {
                yielded.Add(message.Value<string>("content"));
            }

            Assert.Equal(new[] { "new", "on-cutoff" }, yielded);
        }

        [Fact]
        public async Task EnumerateChannelMessages_FailedFirstPageThrowsAndDoesNotContinue() {
            context.Api.MessageListFailuresRemaining = 1;
            context.Api.AddParentMessage("seed", DateTimeOffset.UtcNow, messageId: "1");

            await Assert.ThrowsAsync<DiscordApiException>(async () => {
                await foreach (var _ in context.Client.EnumerateChannelMessages(FakeDiscordApi.ChannelId, null)) {
                }
            });

            Assert.Equal(1, context.Api.MessageListRequestCount);
            Assert.Equal(0, context.Api.DeleteRequestCount);
        }

        [Fact]
        public async Task EnumerateChannelMessages_StuckCursorStopsWithoutDeleting() {
            for (var i = 0; i < 100; i++) {
                context.Api.AddParentMessage("blank-id", DateTimeOffset.UtcNow.AddMinutes(-i), messageId: " ");
            }

            var count = 0;
            await foreach (var _ in context.Client.EnumerateChannelMessages(FakeDiscordApi.ChannelId, null)) {
                count++;
            }

            Assert.Equal(0, count);
            Assert.Equal(1, context.Api.MessageListRequestCount);
        }

        [Fact]
        public async Task EnumerateChannelMessages_HonorsCancellation() {
            context.Api.AddParentMessage("one", DateTimeOffset.UtcNow, messageId: "1");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
                await foreach (var _ in context.Client.EnumerateChannelMessages(FakeDiscordApi.ChannelId, null, cts.Token)) {
                }
            });
        }
    }
}
