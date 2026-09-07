using DaleGhent.NINA.GroundStation.DiscordWebhook;
using System;
using System.Net.Http;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    internal sealed class DiscordTestContext : IDisposable {
        public DiscordTestContext() {
            DiscordClient.SuppressUserNotifications = true;
            DiscordClient.ResetBotUserCache();
            Settings = new FakeDiscordSettings();
            DiscordSettings.Current = Settings;
            Api = new FakeDiscordApi();
            HttpClient = new HttpClient(Api, disposeHandler: true);
            Client = new DiscordClient(HttpClient);
            SessionThreads = new DiscordSessionThreads(Client);
            Cleanup = new DiscordThreadCleanup(Client);
            Common = new DiscordWebhookCommon(Client);
        }

        public FakeDiscordSettings Settings { get; }
        public FakeDiscordApi Api { get; }
        public HttpClient HttpClient { get; }
        public DiscordClient Client { get; }
        public DiscordSessionThreads SessionThreads { get; }
        public DiscordThreadCleanup Cleanup { get; }
        public DiscordWebhookCommon Common { get; }

        public void Dispose() {
            HttpClient.Dispose();
            DiscordSettings.Reset();
            DiscordClient.ResetBotUserCache();
            DiscordClient.SuppressUserNotifications = false;
        }
    }
}
