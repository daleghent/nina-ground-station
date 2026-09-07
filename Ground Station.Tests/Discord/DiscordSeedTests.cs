using DaleGhent.NINA.GroundStation.DiscordWebhook;
using Xunit;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    [Collection("Discord")]
    public sealed class DiscordSeedTests {
        private const string ThreadName = "2026-08-23";

        [Fact]
        public void BuildSeedText_PrefixesInvisibleMarker() {
            var seed = DiscordSeed.BuildSeedText(ThreadName);

            Assert.StartsWith("\u2060\u2063", seed);
            Assert.EndsWith(ThreadName, seed);
            Assert.True(DiscordSeed.HasSeedMarker(seed));
            Assert.False(DiscordSeed.IsFailedSeedText(seed));
        }

        [Fact]
        public void BuildFailedSeedText_IsStillASeedAndMatchesThreadName() {
            var failed = DiscordSeed.BuildFailedSeedText(ThreadName);

            Assert.True(DiscordSeed.HasSeedMarker(failed));
            Assert.True(DiscordSeed.IsFailedSeedText(failed));
            Assert.True(DiscordSeed.SeedTextMatchesThreadName(failed, ThreadName));
            Assert.True(DiscordSeed.SeedTextMatchesThreadName(DiscordSeed.NormalizeSeedText(failed), ThreadName));
        }

        [Fact]
        public void IsDirectChannelPost_IsOwnedWebhookWithoutSeedMarker() {
            const string webhookId = "webhook1";

            Assert.True(DiscordSeed.IsDirectChannelPost(webhookId, webhookId, "09/06/2026 - 01:53:58  -  IMAGING:   Completely Finished NGC 7479"));
            Assert.True(DiscordSeed.IsDirectChannelPost(webhookId, webhookId, "IMAGING: Start Processing"));
            Assert.True(DiscordSeed.IsDirectChannelPost(webhookId, webhookId, null));
            Assert.False(DiscordSeed.IsDirectChannelPost(webhookId, webhookId, DiscordSeed.BuildSeedText(ThreadName)));
            Assert.False(DiscordSeed.IsDirectChannelPost("someone-else", webhookId, "IMAGING: Start Processing"));
            Assert.False(DiscordSeed.IsDirectChannelPost(null, webhookId, "IMAGING: Start Processing"));
            Assert.False(DiscordSeed.IsOwnedWebhook(webhookId, "other"));
            Assert.True(DiscordSeed.IsOwnedWebhook(webhookId, webhookId));
        }

        [Fact]
        public void HasSeedMarker_RejectsHumanChannelPosts() {
            Assert.False(DiscordSeed.HasSeedMarker(null));
            Assert.False(DiscordSeed.HasSeedMarker(string.Empty));
            Assert.False(DiscordSeed.HasSeedMarker(ThreadName));
            Assert.False(DiscordSeed.HasSeedMarker("❌ " + ThreadName));
            Assert.False(DiscordSeed.HasSeedMarker(" " + DiscordSeed.BuildSeedText(ThreadName)));
        }

        [Fact]
        public void SeedTextMatchesThreadName_IsOrdinalAndRejectsEmpty() {
            var seed = DiscordSeed.BuildSeedText(ThreadName);

            Assert.True(DiscordSeed.SeedTextMatchesThreadName(seed, ThreadName));
            Assert.True(DiscordSeed.SeedTextMatchesThreadName(ThreadName, ThreadName));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName(seed, "2026-08-24"));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName(seed, threadName: "2026-08-23 "));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName(DiscordSeed.BuildSeedText("Night"), "NIGHT"));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName(null, ThreadName));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName(seed, null));
            Assert.False(DiscordSeed.SeedTextMatchesThreadName("\u2060\u2063", ThreadName));
        }

        [Fact]
        public void NormalizeSeedText_StripsMarkerOnce() {
            Assert.Equal(ThreadName, DiscordSeed.NormalizeSeedText(DiscordSeed.BuildSeedText(ThreadName)));
            Assert.Equal("❌ " + ThreadName, DiscordSeed.NormalizeSeedText(DiscordSeed.BuildFailedSeedText(ThreadName)));
            Assert.Equal(ThreadName, DiscordSeed.NormalizeSeedText(ThreadName));
            Assert.Null(DiscordSeed.NormalizeSeedText(null));
            Assert.Equal("   ", DiscordSeed.NormalizeSeedText("   "));
        }
    }
}
