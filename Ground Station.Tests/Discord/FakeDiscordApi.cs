using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    internal sealed class CapturedDiscordRequest {
        public CapturedDiscordRequest(HttpMethod method, string url, string body) {
            Method = method;
            Url = url;
            Body = body;
        }

        public HttpMethod Method { get; }
        public string Url { get; }
        public string Body { get; }
    }

    internal sealed class FakeDiscordMessage {
        public string Id { get; set; }
        public string ChannelId { get; set; }
        public string Content { get; set; }
        public string WebhookId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string ThreadId { get; set; }
        public string ThreadName { get; set; }
        public JToken Embeds { get; set; }
    }

    internal sealed class FakeDiscordChannel {
        public string Id { get; set; }
        public int Type { get; set; }
        public string ParentId { get; set; }
        public string Name { get; set; }
        public string OwnerId { get; set; }
        public bool Archived { get; set; }
        public bool Locked { get; set; }
        public string GuildId { get; set; }
    }

    internal sealed class FakeDiscordApi : HttpMessageHandler {
        public const string GuildId = "guild1";
        public const string ChannelId = "channel1";
        public const string WebhookId = "webhook1";
        public const string BotUserId = "bot1";
        public const string WebhookUrl = "https://discord.com/api/webhooks/webhook1/secret-token";

        private readonly object sync = new();
        private long nextId = 1;

        public int ParentChannelType { get; set; } = 0;
        public int RemainingRateLimits { get; set; }
        public int MessageListFailuresRemaining { get; set; }
        public bool RejectWebhookThreadName { get; set; }

        public List<CapturedDiscordRequest> Requests { get; } = new();
        public List<FakeDiscordMessage> Messages { get; } = new();
        public Dictionary<string, FakeDiscordChannel> Channels { get; } = new(StringComparer.Ordinal);

        public FakeDiscordApi() {
            Channels[ChannelId] = new FakeDiscordChannel {
                Id = ChannelId,
                Type = ParentChannelType,
                GuildId = GuildId,
                Name = "imaging",
            };
        }

        public static string ToSnowflake(DateTimeOffset timestamp) {
            const long discordEpoch = 1420070400000L;
            return ((timestamp.ToUnixTimeMilliseconds() - discordEpoch) << 22).ToString();
        }

        public FakeDiscordMessage AddParentMessage(
            string content,
            DateTimeOffset createdAt,
            string threadId = null,
            string threadName = null,
            string webhookId = WebhookId,
            string messageId = null,
            object embeds = null) {
            lock (sync) {
                var message = new FakeDiscordMessage {
                    Id = messageId ?? $"{ToSnowflake(createdAt)}{Interlocked.Increment(ref nextId)}",
                    ChannelId = ChannelId,
                    Content = content,
                    WebhookId = webhookId,
                    CreatedAt = createdAt,
                    ThreadId = threadId,
                    ThreadName = threadName,
                    Embeds = embeds == null ? null : JToken.FromObject(embeds),
                };
                Messages.Add(message);
                return message;
            }
        }

        public FakeDiscordChannel AddThread(
            string threadId,
            string name,
            string ownerId = BotUserId,
            bool archived = false,
            bool locked = false,
            int type = 11) {
            lock (sync) {
                var thread = new FakeDiscordChannel {
                    Id = threadId,
                    Type = type,
                    ParentId = ChannelId,
                    Name = name,
                    OwnerId = ownerId,
                    Archived = archived,
                    Locked = locked,
                    GuildId = GuildId,
                };
                Channels[threadId] = thread;
                return thread;
            }
        }

        public int DeleteRequestCount {
            get {
                lock (sync) {
                    return Requests.Count(request => request.Method == HttpMethod.Delete);
                }
            }
        }

        public int MessageListRequestCount {
            get {
                lock (sync) {
                    return Requests.Count(request =>
                        request.Method == HttpMethod.Get && request.Url.Contains("/messages?", StringComparison.Ordinal));
                }
            }
        }

        public List<string> DeletedUrls {
            get {
                lock (sync) {
                    return Requests
                        .Where(request => request.Method == HttpMethod.Delete)
                        .Select(request => request.Url)
                        .ToList();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync();
            var url = request.RequestUri?.ToString() ?? string.Empty;

            lock (sync) {
                Requests.Add(new CapturedDiscordRequest(request.Method, url, body));
            }

            if (RemainingRateLimits > 0) {
                RemainingRateLimits--;
                return Json(429, new { retry_after = 0.001 });
            }

            var uri = request.RequestUri;
            var path = uri.AbsolutePath.Trim('/');
            var segments = path.Split('/');
            var before = GetQueryValue(uri.Query, "before");
            var threadId = GetQueryValue(uri.Query, "thread_id");

            if (request.Method == HttpMethod.Get && path.Contains("/messages") && MessageListFailuresRemaining > 0) {
                MessageListFailuresRemaining--;
                return Json(500, new { message = "failed" });
            }

            if (request.Method == HttpMethod.Get && url.StartsWith(WebhookUrl, StringComparison.Ordinal) && !url.Contains("/messages", StringComparison.Ordinal)) {
                lock (sync) {
                    Channels[ChannelId].Type = ParentChannelType;
                }

                return Json(200, new { id = WebhookId, channel_id = ChannelId, guild_id = GuildId });
            }

            if (request.Method == HttpMethod.Get && path == "api/v10/users/@me") {
                return Json(200, new { id = BotUserId });
            }

            if (request.Method == HttpMethod.Get && IsChannelRoot(segments)) {
                return GetChannel(segments[3]);
            }

            if (request.Method == HttpMethod.Get && path.Contains("/messages") && segments.Contains("channels")) {
                return ListMessages(segments[Array.IndexOf(segments, "channels") + 1], before);
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/threads/active", StringComparison.Ordinal)) {
                return ListActiveThreads();
            }

            if (request.Method == HttpMethod.Post && url.StartsWith(WebhookUrl, StringComparison.Ordinal) && !url.Contains("/messages/", StringComparison.Ordinal)) {
                return PostWebhook(threadId, body);
            }

            if (string.Equals(request.Method.Method, "PATCH", StringComparison.OrdinalIgnoreCase)
                && url.StartsWith(WebhookUrl, StringComparison.Ordinal)
                && path.Contains("/messages/")) {
                return EditMessage(segments[^1], body);
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/threads", StringComparison.Ordinal)) {
                return CreateThreadFromMessage(
                    segments[Array.IndexOf(segments, "channels") + 1],
                    segments[Array.IndexOf(segments, "messages") + 1],
                    body);
            }

            if (string.Equals(request.Method.Method, "PATCH", StringComparison.OrdinalIgnoreCase) && IsChannelRoot(segments)) {
                return PatchChannel(segments[3], body);
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/messages/")) {
                return RemoveMessage(segments[^1])
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : Json(404, new { message = "Unknown Message" });
            }

            if (request.Method == HttpMethod.Delete && IsChannelRoot(segments)) {
                return RemoveChannel(segments[3])
                    ? new HttpResponseMessage(HttpStatusCode.NoContent)
                    : Json(404, new { message = "Unknown Channel" });
            }

            return Json(404, new { message = "Unknown fake Discord route", url });
        }

        private static bool IsChannelRoot(string[] segments) {
            return segments.Length == 4
                && segments[0] == "api"
                && segments[1] == "v10"
                && segments[2] == "channels";
        }

        private HttpResponseMessage GetChannel(string channelId) {
            lock (sync) {
                if (!Channels.TryGetValue(channelId, out var channel)) {
                    return Json(404, new { message = "Unknown Channel" });
                }

                if (channel.Type is 10 or 11 or 12) {
                    return Json(200, new {
                        id = channel.Id,
                        type = channel.Type,
                        parent_id = channel.ParentId,
                        name = channel.Name,
                        owner_id = channel.OwnerId,
                        guild_id = channel.GuildId,
                        thread_metadata = new { archived = channel.Archived, locked = channel.Locked },
                    });
                }

                return Json(200, new {
                    id = channel.Id,
                    type = channel.Type,
                    name = channel.Name,
                    guild_id = channel.GuildId,
                });
            }
        }

        private HttpResponseMessage ListMessages(string channelId, string before) {
            lock (sync) {
                IEnumerable<FakeDiscordMessage> ordered = Messages
                    .Where(message => string.Equals(message.ChannelId, channelId, StringComparison.Ordinal))
                    .OrderByDescending(message => ParseId(message.Id));

                if (!string.IsNullOrWhiteSpace(before) && ulong.TryParse(before, out var beforeId)) {
                    ordered = ordered.Where(message => ParseId(message.Id) < beforeId);
                }

                return Json(200, ordered.Take(100).Select(ToMessageJson).ToArray());
            }
        }

        private HttpResponseMessage ListActiveThreads() {
            lock (sync) {
                var threads = Channels.Values
                    .Where(channel => channel.Type is 10 or 11 or 12 && !channel.Archived)
                    .Select(channel => new {
                        id = channel.Id,
                        type = channel.Type,
                        parent_id = channel.ParentId,
                        name = channel.Name,
                        owner_id = channel.OwnerId,
                        guild_id = channel.GuildId,
                        thread_metadata = new { archived = channel.Archived, locked = channel.Locked },
                    })
                    .ToArray();

                return Json(200, new { threads });
            }
        }

        private HttpResponseMessage PostWebhook(string threadId, string body) {
            var payload = ParsePayload(body);
            var threadNameProperty = payload.Property("thread_name");
            var threadName = threadNameProperty?.Value?.Type == JTokenType.Null
                ? null
                : threadNameProperty?.Value?.Value<string>();
            var content = payload.Value<string>("content") ?? string.Empty;

            lock (sync) {
                Channels[ChannelId].Type = ParentChannelType;

                if (threadNameProperty != null && RejectWebhookThreadName) {
                    return Json(400, new {
                        code = 220003,
                        message = "Webhooks can only create threads in forum channels",
                    });
                }

                var createdAt = DateTimeOffset.UtcNow;
                var targetChannelId = ChannelId;

                if (!string.IsNullOrWhiteSpace(threadId)) {
                    targetChannelId = threadId;
                } else if (!string.IsNullOrWhiteSpace(threadName) && ParentChannelType is 15 or 16) {
                    var newThreadId = NewId(createdAt);
                    AddThread(newThreadId, threadName);
                    targetChannelId = newThreadId;
                } else if (threadNameProperty != null && ParentChannelType is 0 or 5) {
                    // Mirrors Discord's text-channel behavior: a present thread_name field,
                    // including null, attaches the webhook message to the active thread.
                    var activeThread = Channels.Values.FirstOrDefault(channel =>
                        channel.Type is 10 or 11 or 12
                        && !channel.Archived
                        && string.Equals(channel.ParentId, ChannelId, StringComparison.Ordinal));
                    if (activeThread != null) {
                        targetChannelId = activeThread.Id;
                    }
                }

                var message = new FakeDiscordMessage {
                    Id = NewId(createdAt),
                    ChannelId = targetChannelId,
                    Content = content,
                    WebhookId = WebhookId,
                    CreatedAt = createdAt,
                };
                Messages.Add(message);

                return Json(200, new {
                    id = message.Id,
                    channel_id = targetChannelId,
                    content,
                    webhook_id = WebhookId,
                    timestamp = createdAt.UtcDateTime.ToString("o"),
                });
            }
        }

        private HttpResponseMessage EditMessage(string messageId, string body) {
            var payload = ParsePayload(body);
            lock (sync) {
                var message = Messages.FirstOrDefault(item => string.Equals(item.Id, messageId, StringComparison.Ordinal));
                if (message == null) {
                    return Json(404, new { message = "Unknown Message" });
                }

                message.Content = payload.Value<string>("content") ?? message.Content;
                return Json(200, ToMessageJson(message));
            }
        }

        private HttpResponseMessage CreateThreadFromMessage(string channelId, string messageId, string body) {
            var payload = ParsePayload(body);
            var name = payload.Value<string>("name") ?? "thread";

            lock (sync) {
                var message = Messages.FirstOrDefault(item => string.Equals(item.Id, messageId, StringComparison.Ordinal));
                var thread = new FakeDiscordChannel {
                    Id = messageId,
                    Type = 11,
                    ParentId = channelId,
                    Name = name,
                    OwnerId = BotUserId,
                    GuildId = GuildId,
                };
                Channels[messageId] = thread;
                if (message != null) {
                    message.ThreadId = messageId;
                    message.ThreadName = name;
                }

                return Json(200, new {
                    id = thread.Id,
                    type = thread.Type,
                    parent_id = thread.ParentId,
                    name = thread.Name,
                    owner_id = thread.OwnerId,
                    guild_id = thread.GuildId,
                    thread_metadata = new { archived = false, locked = false },
                });
            }
        }

        private HttpResponseMessage PatchChannel(string channelId, string body) {
            var payload = ParsePayload(body);
            lock (sync) {
                if (!Channels.TryGetValue(channelId, out var channel)) {
                    return Json(404, new { message = "Unknown Channel" });
                }

                if (payload["archived"] != null) {
                    channel.Archived = payload.Value<bool>("archived");
                }

                if (payload["locked"] != null) {
                    channel.Locked = payload.Value<bool>("locked");
                }
            }

            return GetChannel(channelId);
        }

        private bool RemoveMessage(string messageId) {
            lock (sync) {
                return Messages.RemoveAll(message => string.Equals(message.Id, messageId, StringComparison.Ordinal)) > 0;
            }
        }

        private bool RemoveChannel(string channelId) {
            lock (sync) {
                if (!Channels.Remove(channelId)) {
                    return false;
                }

                foreach (var message in Messages.Where(item => string.Equals(item.ThreadId, channelId, StringComparison.Ordinal))) {
                    message.ThreadId = null;
                    message.ThreadName = null;
                }

                return true;
            }
        }

        private string NewId(DateTimeOffset timestamp) {
            return $"{ToSnowflake(timestamp)}{Interlocked.Increment(ref nextId)}";
        }

        private static object ToMessageJson(FakeDiscordMessage message) {
            object thread = string.IsNullOrWhiteSpace(message.ThreadId)
                ? null
                : new { id = message.ThreadId, name = message.ThreadName };

            return new {
                id = message.Id,
                channel_id = message.ChannelId,
                content = message.Content,
                webhook_id = message.WebhookId,
                timestamp = message.CreatedAt.UtcDateTime.ToString("o"),
                embeds = message.Embeds,
                thread,
            };
        }

        private static JObject ParsePayload(string body) {
            if (string.IsNullOrWhiteSpace(body)) {
                return new JObject();
            }

            try {
                if (JToken.Parse(body) is JObject obj) {
                    return obj;
                }
            } catch {
            }

            var start = body.IndexOf('{');
            var end = body.LastIndexOf('}');
            if (start >= 0 && end > start) {
                try {
                    return JObject.Parse(body.Substring(start, end - start + 1));
                } catch {
                }
            }

            return new JObject();
        }

        private static ulong ParseId(string id) {
            return ulong.TryParse(id, out var value) ? value : 0;
        }

        private static string GetQueryValue(string query, string key) {
            if (string.IsNullOrEmpty(query)) {
                return null;
            }

            foreach (var pair in query.TrimStart('?').Split('&')) {
                var parts = pair.Split('=');
                if (parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.Ordinal)) {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            return null;
        }

        private static HttpResponseMessage Json(int statusCode, object payload) {
            return new HttpResponseMessage((HttpStatusCode)statusCode) {
                Content = new StringContent(JsonConvert.SerializeObject(payload, new JsonSerializerSettings {
                    NullValueHandling = NullValueHandling.Ignore,
                }), Encoding.UTF8, "application/json"),
            };
        }
    }
}
