using DaleGhent.NINA.GroundStation.DiscordWebhook;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    [Collection("Discord")]
    public sealed class DiscordClientTests : IDisposable {
        private readonly DiscordTestContext context = new();

        public void Dispose() => context.Dispose();

        [Fact]
        public void GetMessageCreatedAt_PrefersTimestampOverSnowflake() {
            var createdAt = DateTimeOffset.Parse("2026-08-23T01:02:03Z", CultureInfo.InvariantCulture);
            var message = new JObject {
                ["timestamp"] = createdAt.UtcDateTime.ToString("o"),
                ["id"] = FakeDiscordApi.ToSnowflake(createdAt.AddHours(-5)),
            };

            var parsed = DiscordClient.GetMessageCreatedAt(message);

            Assert.Equal(createdAt.ToUnixTimeSeconds(), parsed.Value.ToUnixTimeSeconds());
        }

        [Fact]
        public void GetMessageCreatedAt_FallsBackToSnowflake() {
            var createdAt = DateTimeOffset.Parse("2026-01-02T03:04:05Z", CultureInfo.InvariantCulture);
            var message = new JObject { ["id"] = FakeDiscordApi.ToSnowflake(createdAt) };

            var parsed = DiscordClient.GetMessageCreatedAt(message);

            Assert.True(Math.Abs((parsed.Value - createdAt).TotalSeconds) < 2);
        }

        [Fact]
        public void GetMessageCreatedAt_TreatsUnspecifiedDateAsUtc() {
            var unspecified = new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Unspecified);
            var message = new JObject { ["timestamp"] = unspecified };

            var parsed = DiscordClient.GetMessageCreatedAt(message);

            Assert.Equal(DateTimeKind.Utc, parsed.Value.UtcDateTime.Kind);
            Assert.Equal(12, parsed.Value.UtcDateTime.Hour);
        }

        [Fact]
        public void GetMessageCreatedAt_ReturnsNullWhenNothingIsParseable() {
            Assert.Null(DiscordClient.GetMessageCreatedAt(new JObject()));
            Assert.Null(DiscordClient.GetMessageCreatedAt(null));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(5, true)]
        [InlineData(15, false)]
        [InlineData(16, false)]
        [InlineData(11, false)]
        public void RequiresBotThreadCreation_OnlyTextAndAnnouncement(int channelType, bool expected) {
            var channel = new JObject { ["type"] = channelType };
            Assert.Equal(expected, DiscordClient.RequiresBotThreadCreation(channel));
        }

        [Fact]
        public void RequiresBotThreadCreation_ThrowsWhenTypeIsMissing() {
            Assert.Throws<DiscordApiException>(() => DiscordClient.RequiresBotThreadCreation(new JObject()));
        }

        [Fact]
        public void GetRequiredString_ThrowsForBlankValues() {
            var obj = new JObject { ["id"] = "   " };
            var ex = Assert.Throws<DiscordApiException>(() =>
                DiscordClient.GetRequiredString(obj, "id", "Discord Bot Error", "missing id"));
            Assert.Equal("missing id", ex.Message);
        }

        [Theory]
        [InlineData("https://discord.com/api/webhooks/123/token", true, null, "https://discord.com/api/webhooks/123/token?wait=true")]
        [InlineData("https://discord.com/api/webhooks/123/token?thread_id=old-thread", true, null, "https://discord.com/api/webhooks/123/token?wait=true")]
        [InlineData("https://discord.com/api/webhooks/123/token?wait=true&thread_id=old-thread", true, "new-thread", "https://discord.com/api/webhooks/123/token?wait=true&thread_id=new-thread")]
        public void BuildWebhookUrl_ReplacesWaitAndThreadId(string webhookUrl, bool waitForResponse, string threadId, string expected) {
            Assert.Equal(expected, DiscordClient.BuildWebhookUrl(webhookUrl, waitForResponse, threadId));
        }

        [Fact]
        public void SanitizeDiscordUrl_RedactsWebhookToken() {
            var sanitized = DiscordClient.SanitizeDiscordUrl("https://discord.com/api/webhooks/123/super-secret?wait=true");
            Assert.Contains("/webhooks/123/REDACTED", sanitized);
            Assert.DoesNotContain("super-secret", sanitized);
            Assert.Contains("wait=true", sanitized);
        }

        [Fact]
        public async Task GetWebhookMetadata_ReturnsIds() {
            var metadata = await context.Client.GetWebhookMetadata(FakeDiscordApi.WebhookUrl);

            Assert.Equal(FakeDiscordApi.WebhookId, metadata.WebhookId);
            Assert.Equal(FakeDiscordApi.ChannelId, metadata.ChannelId);
            Assert.Equal(FakeDiscordApi.GuildId, metadata.GuildId);
            Assert.Equal(FakeDiscordApi.WebhookUrl, metadata.Url);
        }

        [Fact]
        public async Task RateLimitRetry_SucceedsAfter429() {
            context.Api.RemainingRateLimits = 2;

            var metadata = await context.Client.GetWebhookMetadata(FakeDiscordApi.WebhookUrl);

            Assert.Equal(FakeDiscordApi.WebhookId, metadata.WebhookId);
            Assert.True(context.Api.Requests.Count >= 3);
        }

        [Fact]
        public async Task DeleteChannelMessage_Treats404AsSuccess() {
            await context.Client.DeleteChannelMessage(FakeDiscordApi.ChannelId, "missing");
        }

        [Fact]
        public async Task TryGetBotUserId_CachesUntilTokenChanges() {
            var first = await context.Client.TryGetBotUserId();
            var second = await context.Client.TryGetBotUserId();
            context.Settings.BotToken = "other-token";
            var third = await context.Client.TryGetBotUserId();

            Assert.Equal(FakeDiscordApi.BotUserId, first);
            Assert.Equal(first, second);
            Assert.Equal(2, context.Api.Requests.Count(request => request.Url.EndsWith("/users/@me", StringComparison.Ordinal)));
            Assert.Equal(FakeDiscordApi.BotUserId, third);
        }
    }
}
