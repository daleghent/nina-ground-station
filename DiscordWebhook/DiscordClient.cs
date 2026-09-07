#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    internal sealed class DiscordClient {
        private static readonly HttpClient SharedHttpClient = new();
        private static readonly object botUserCacheLock = new();
        private static string cachedBotUserId;
        private static string cachedBotUserToken;
        private readonly HttpClient httpClient;

        private const int ChannelMessagePageSize = 100;
        private const int MaxDiscordRateLimitRetries = 5;
        private const int PublicTextChannelType = 0;
        private const int AnnouncementChannelType = 5;
        private static readonly TimeSpan DefaultDiscordRateLimitDelay = TimeSpan.FromSeconds(1);

        internal static bool SuppressUserNotifications { get; set; }

        private static readonly JsonSerializerSettings JsonSettings = new() {
            NullValueHandling = NullValueHandling.Ignore,
        };

        internal DiscordClient() : this(SharedHttpClient) {
        }

        internal DiscordClient(HttpClient httpClient) {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        internal static void ResetBotUserCache() {
            lock (botUserCacheLock) {
                cachedBotUserId = null;
                cachedBotUserToken = null;
            }
        }

        internal static DiscordApiException CreateApiException(string title, string message, Exception innerException = null, bool notifyUser = true) {
            Logger.Error(innerException == null
                ? $"{title}: {message}"
                : $"{title}: {message} {innerException.Message}");

            if (notifyUser && !SuppressUserNotifications) {
                Notification.ShowExternalError(message, title);
            }

            return new DiscordApiException(message, innerException);
        }

        internal static string GetRequiredString(JObject value, string propertyName, string title, string failureMessage, bool notifyOnFailure = true) {
            var propertyValue = value?[propertyName]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(propertyValue)) {
                return propertyValue;
            }

            throw CreateApiException(title, failureMessage, notifyUser: notifyOnFailure);
        }

        internal static DateTimeOffset? GetMessageCreatedAt(JObject message) {
            var timestampToken = message?["timestamp"];
            if (timestampToken != null) {
                switch (timestampToken.Type) {
                    case JTokenType.Date:
                        if (timestampToken.Value<DateTime?>() is DateTime timestampDateTime) {
                            if (timestampDateTime.Kind == DateTimeKind.Unspecified) {
                                timestampDateTime = DateTime.SpecifyKind(timestampDateTime, DateTimeKind.Utc);
                            }

                            return new DateTimeOffset(timestampDateTime.ToUniversalTime(), TimeSpan.Zero);
                        }
                        break;

                    default:
                        var timestampString = timestampToken.Value<string>();
                        if (!string.IsNullOrWhiteSpace(timestampString)
                            && DateTimeOffset.TryParse(timestampString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedTimestamp)) {
                            return parsedTimestamp.ToUniversalTime();
                        }
                        break;
                }
            }

            var messageId = message?["id"]?.Value<string>();
            if (ulong.TryParse(messageId, out var snowflake)) {
                var millisecondsSinceDiscordEpoch = (long)(snowflake >> 22);
                return DateTimeOffset.FromUnixTimeMilliseconds(millisecondsSinceDiscordEpoch + 1420070400000L);
            }

            return null;
        }

        internal async Task<WebhookMetadata> GetWebhookMetadata(string webhookUrl, bool notifyOnFailure = true) {
            var (response, responseBody) = await SendRequestWithRateLimitRetry(
                () => new HttpRequestMessage(HttpMethod.Get, webhookUrl),
                "webhook-metadata",
                HttpMethod.Get,
                webhookUrl,
                "Failed to fetch Discord webhook metadata",
                notifyOnFailure);

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    throw CreateApiException("Discord Webhook Error", $"Discord webhook metadata request returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, notifyOnFailure);
                }

                JObject responseJson;
                try {
                    responseJson = JObject.Parse(responseBody);
                } catch (Exception ex) {
                    throw CreateApiException("Discord Webhook Error", $"Discord returned invalid webhook metadata: {ex.Message}", ex, notifyOnFailure);
                }

                var webhookId = GetRequiredString(responseJson, "id", "Discord Webhook Error", "Discord did not return an id for the configured webhook.", notifyOnFailure);
                var channelId = GetRequiredString(responseJson, "channel_id", "Discord Webhook Error", "Discord did not return a channel id for the configured webhook.", notifyOnFailure);
                var guildId = GetRequiredString(responseJson, "guild_id", "Discord Webhook Error", "Discord did not return a guild id for the configured webhook.", notifyOnFailure);
                return new WebhookMetadata(webhookUrl, webhookId, channelId, guildId);
            }
        }

        internal Task<JObject> GetChannel(string channelId, bool notifyOnFailure = true) {
            return SendBotRequest(HttpMethod.Get, $"https://discord.com/api/v10/channels/{channelId}", notifyOnFailure: notifyOnFailure);
        }

        internal static bool RequiresBotThreadCreation(JObject parentChannel) {
            var channelType = parentChannel["type"]?.Value<int>()
                ?? throw CreateApiException("Discord Bot Error", "Discord did not return a channel type for the webhook channel.");
            return channelType == PublicTextChannelType || channelType == AnnouncementChannelType;
        }

        /// <summary>
        /// Pages parent-channel history newest-first. When <paramref name="untilUtc"/> is set, stops
        /// after the cutoff is actually hit rather than after a single 100-message page. A failed
        /// first page throws; a stuck cursor ends the enumeration without deleting anything.
        /// </summary>
        internal async IAsyncEnumerable<JObject> EnumerateChannelMessages(
            string channelId,
            DateTimeOffset? untilUtc,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            string beforeMessageId = null;

            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                var previousCursor = beforeMessageId;
                var messages = await GetChannelMessages(channelId, beforeMessageId);

                foreach (var message in messages.OfType<JObject>()) {
                    var messageId = message["id"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(messageId)) {
                        continue;
                    }

                    beforeMessageId = messageId;

                    var createdAt = GetMessageCreatedAt(message);
                    if (untilUtc.HasValue && createdAt.HasValue && createdAt.Value < untilUtc.Value) {
                        Logger.Debug($"Discord parent message scan channelId='{channelId}' reached archive cutoff at messageId='{messageId}' createdAtUtc='{createdAt.Value:O}' cutoffUtc='{untilUtc.Value:O}'");
                        yield break;
                    }

                    yield return message;
                }

                if (string.Equals(beforeMessageId, previousCursor, StringComparison.Ordinal)) {
                    break;
                }

                if (messages.Count < ChannelMessagePageSize) {
                    break;
                }
            }
        }

        internal async Task<string> TryGetBotUserId() {
            var botToken = DiscordSettings.Current.BotToken;
            lock (botUserCacheLock) {
                if (!string.IsNullOrWhiteSpace(cachedBotUserId) && string.Equals(cachedBotUserToken, botToken, StringComparison.Ordinal)) {
                    return cachedBotUserId;
                }
            }

            try {
                var botUser = await SendBotRequest(HttpMethod.Get, "https://discord.com/api/v10/users/@me", notifyOnFailure: false);
                var botUserId = botUser["id"]?.Value<string>();
                lock (botUserCacheLock) {
                    cachedBotUserId = botUserId;
                    cachedBotUserToken = botToken;
                }
                return botUserId;
            } catch (Exception ex) {
                Logger.Warning($"Discord could not identify its own bot user, so thread cleanup will be skipped. {ex.Message}");
                return null;
            }
        }

        internal async Task<JObject> PostWebhook(string webhookUrl, object payload, bool waitForResponse, string threadId = null, byte[] attachmentBytes = null, string attachmentFileName = null, bool notifyOnFailure = true) {
            var requestUrl = BuildWebhookUrl(webhookUrl, waitForResponse, threadId);

            var (response, responseBody) = await SendRequestWithRateLimitRetry(() => {
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                if (attachmentBytes != null) {
                    var multipartContent = new MultipartFormDataContent();
                    multipartContent.Add(new StringContent(SerializeJson(payload), Encoding.UTF8, "application/json"), "payload_json");

                    var fileContent = new ByteArrayContent(attachmentBytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    multipartContent.Add(fileContent, "files[0]", attachmentFileName);
                    request.Content = multipartContent;
                } else {
                    request.Content = new StringContent(SerializeJson(payload), Encoding.UTF8, "application/json");
                }

                return request;
            }, "webhook", HttpMethod.Post, requestUrl, "Failed to send webhook request to Discord", notifyOnFailure);

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    throw CreateApiException("Discord Webhook Error", $"Discord webhook API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, notifyOnFailure);
                }

                if (!waitForResponse || string.IsNullOrWhiteSpace(responseBody)) {
                    return null;
                }

                try {
                    return JObject.Parse(responseBody);
                } catch (Exception ex) {
                    throw CreateApiException("Discord Webhook Error", $"Discord returned an invalid webhook response: {ex.Message}", ex, notifyOnFailure);
                }
            }
        }

        internal async Task<JObject> EditWebhookMessage(string webhookUrl, string messageId, object payload, bool notifyOnFailure = true) {
            var requestUrl = $"{webhookUrl}/messages/{Uri.EscapeDataString(messageId)}";
            var patchMethod = new HttpMethod("PATCH");
            var (response, responseBody) = await SendRequestWithRateLimitRetry(() => {
                var request = new HttpRequestMessage(patchMethod, requestUrl);
                request.Content = new StringContent(SerializeJson(payload), Encoding.UTF8, "application/json");
                return request;
            }, "webhook-edit", patchMethod, requestUrl, "Failed to edit Discord webhook message", notifyOnFailure);

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    throw CreateApiException("Discord Webhook Error", $"Discord webhook message edit returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, notifyOnFailure);
                }

                if (string.IsNullOrWhiteSpace(responseBody)) {
                    return new JObject();
                }

                try {
                    return JObject.Parse(responseBody);
                } catch (Exception ex) {
                    throw CreateApiException("Discord Webhook Error", $"Discord returned an invalid webhook message edit response: {ex.Message}", ex, notifyOnFailure);
                }
            }
        }

        internal async Task DeleteChannelMessage(string channelId, string messageId, bool notifyOnFailure = true) {
            try {
                await SendBotRequest(HttpMethod.Delete, $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}", notifyOnFailure: notifyOnFailure);
            } catch (DiscordApiException ex) when (ex.Message.Contains("returned 404", StringComparison.Ordinal)) {
                Logger.Info($"Discord cleanup delete for parent message '{messageId}' in channel '{channelId}' returned 404 because the message was already gone. Treating it as successfully removed.");
            }
        }

        internal static string BuildWebhookUrl(string webhookUrl, bool waitForResponse, string threadId) {
            if (string.IsNullOrWhiteSpace(webhookUrl)) {
                return webhookUrl;
            }

            var parts = webhookUrl.Split(['?'], 2);
            var baseUrl = parts[0];
            var query = new List<string>();

            if (parts.Length > 1) {
                foreach (var pair in parts[1].Split('&')) {
                    if (string.IsNullOrEmpty(pair)) {
                        continue;
                    }

                    // The stored webhook URL must not pin wait/thread_id; each send owns those.
                    if (pair.StartsWith("wait=", StringComparison.OrdinalIgnoreCase)
                        || pair.StartsWith("thread_id=", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    query.Add(pair);
                }
            }

            if (waitForResponse) {
                query.Add("wait=true");
            }

            if (!string.IsNullOrWhiteSpace(threadId)) {
                query.Add($"thread_id={Uri.EscapeDataString(threadId)}");
            }

            if (query.Count == 0) {
                return baseUrl;
            }

            return $"{baseUrl}?{string.Join("&", query)}";
        }

        internal static string SerializeJson(object payload) {
            return JsonConvert.SerializeObject(payload, JsonSettings);
        }

        internal async Task<JObject> SendBotRequest(HttpMethod method, string url, object payload = null, bool notifyOnFailure = true) {
            var (response, responseBody) = await SendRequestWithRateLimitRetry(() => {
                var request = new HttpRequestMessage(method, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bot", DiscordSettings.Current.BotToken);

                if (payload != null) {
                    request.Content = new StringContent(SerializeJson(payload), Encoding.UTF8, "application/json");
                }

                return request;
            }, "bot", method, url, "Failed to send Discord bot request", notifyOnFailure);

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    throw CreateApiException("Discord Bot Error", $"Discord bot API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", null, notifyOnFailure);
                }

                if (string.IsNullOrWhiteSpace(responseBody)) {
                    return new JObject();
                }

                try {
                    return JObject.Parse(responseBody);
                } catch (Exception ex) {
                    throw CreateApiException("Discord Bot Error", $"Discord returned an invalid bot API response: {ex.Message}", ex, notifyOnFailure);
                }
            }
        }

        internal async Task<(HttpResponseMessage Response, string ResponseBody)> SendRequestWithRateLimitRetry(Func<HttpRequestMessage> requestFactory, string source, HttpMethod method, string url, string failureOperation, bool notifyOnFailure) {
            for (var attempt = 1; ; attempt++) {
                using var request = requestFactory();
                HttpResponseMessage response;

                try {
                    response = await httpClient.SendAsync(request);
                } catch (Exception ex) {
                    throw CreateApiException(source == "bot" ? "Discord Bot Error" : "Discord Webhook Error", $"{failureOperation}: {ex.Message}", ex, notifyOnFailure);
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                LogDiscordApiResponse(source, method, url, (int)response.StatusCode, responseBody);

                if ((int)response.StatusCode != 429 || attempt > MaxDiscordRateLimitRetries) {
                    return (response, responseBody);
                }

                var retryDelay = GetDiscordRetryDelay(response, responseBody);
                Logger.Warning($"Discord rate limit source='{source}' method='{method}' url='{SanitizeDiscordUrl(url)}' attempt='{attempt}' retryDelayMs='{Math.Ceiling(retryDelay.TotalMilliseconds)}'");
                response.Dispose();
                await Task.Delay(retryDelay);
            }
        }

        private async Task<JArray> GetChannelMessages(string channelId, string beforeMessageId = null) {
            var url = $"https://discord.com/api/v10/channels/{channelId}/messages?limit={ChannelMessagePageSize}";
            if (!string.IsNullOrWhiteSpace(beforeMessageId)) {
                url += $"&before={Uri.EscapeDataString(beforeMessageId)}";
            }

            var (response, responseBody) = await SendRequestWithRateLimitRetry(() => {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bot", DiscordSettings.Current.BotToken);
                return request;
            }, "bot", HttpMethod.Get, url, $"Failed to list messages for Discord channel {channelId}", notifyOnFailure: true);

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    throw CreateApiException("Discord Bot Error", $"Discord channel message list returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}", notifyUser: true);
                }

                if (string.IsNullOrWhiteSpace(responseBody)) {
                    return [];
                }

                try {
                    return JArray.Parse(responseBody);
                } catch (Exception ex) {
                    throw CreateApiException("Discord Bot Error", $"Discord returned an invalid channel message list response: {ex.Message}", ex, notifyUser: true);
                }
            }
        }

        private static TimeSpan GetDiscordRetryDelay(HttpResponseMessage response, string responseBody) {
            if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfterDelta && retryAfterDelta > TimeSpan.Zero) {
                return retryAfterDelta;
            }

            if (response.Headers.RetryAfter?.Date is DateTimeOffset retryAfterDate) {
                var retryDelay = retryAfterDate - DateTimeOffset.UtcNow;
                if (retryDelay > TimeSpan.Zero) {
                    return retryDelay;
                }
            }

            if (response.Headers.TryGetValues("Retry-After", out var retryAfterValues)) {
                var retryAfterValue = retryAfterValues.FirstOrDefault();
                if (double.TryParse(retryAfterValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var retryAfterSeconds) && retryAfterSeconds > 0) {
                    return TimeSpan.FromSeconds(retryAfterSeconds);
                }
            }

            try {
                var responseJson = JObject.Parse(responseBody);
                var retryAfterToken = responseJson["retry_after"];
                if (retryAfterToken != null && double.TryParse(retryAfterToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var retryAfterSeconds) && retryAfterSeconds > 0) {
                    return TimeSpan.FromSeconds(retryAfterSeconds);
                }
            } catch {
            }

            return DefaultDiscordRateLimitDelay;
        }

        private static void LogDiscordApiResponse(string source, HttpMethod method, string url, int statusCode, string responseBody) {
            Logger.Debug($"Discord API response source='{source}' method='{method}' url='{SanitizeDiscordUrl(url)}' statusCode='{statusCode}' bodySummary='{SummarizeDiscordApiResponseBody(responseBody)}'");
        }

        internal static string SanitizeDiscordUrl(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return string.Empty;
            }

            try {
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Trim('/').Split('/');
                var webhookIndex = Array.FindIndex(segments, segment => string.Equals(segment, "webhooks", StringComparison.OrdinalIgnoreCase));

                if (webhookIndex >= 0 && segments.Length > webhookIndex + 2) {
                    segments[webhookIndex + 2] = "REDACTED";
                }

                var sanitizedPath = string.Join("/", segments);
                return $"{uri.Scheme}://{uri.Host}/{sanitizedPath}{uri.Query}";
            } catch {
                return url;
            }
        }

        private static string SummarizeDiscordApiResponseBody(string responseBody) {
            if (string.IsNullOrWhiteSpace(responseBody)) {
                return "empty";
            }

            try {
                var token = JToken.Parse(responseBody);
                return token switch {
                    JObject obj => SummarizeDiscordObjectBody(obj),
                    JArray array => $"json-array count={array.Count}",
                    _ => $"json-{token.Type.ToString().ToLowerInvariant()}"
                };
            } catch {
                return $"non-json length={responseBody.Length}";
            }
        }

        private static string SummarizeDiscordObjectBody(JObject obj) {
            var summaryParts = new List<string>();
            AddSummaryPart(summaryParts, "keys", string.Join(",", obj.Properties().Select(property => property.Name).Take(12)));
            AddSummaryPart(summaryParts, "id", obj["id"]?.Value<string>());
            AddSummaryPart(summaryParts, "channel_id", obj["channel_id"]?.Value<string>());
            AddSummaryPart(summaryParts, "guild_id", obj["guild_id"]?.Value<string>());
            AddSummaryPart(summaryParts, "parent_id", obj["parent_id"]?.Value<string>());
            AddSummaryPart(summaryParts, "type", obj["type"]?.ToString());
            AddSummaryPart(summaryParts, "name", obj["name"]?.Value<string>());

            if (obj["threads"] is JArray threads) {
                AddSummaryPart(summaryParts, "threadsCount", threads.Count.ToString(CultureInfo.InvariantCulture));
            }

            if (obj["thread_metadata"] is JObject threadMetadata) {
                AddSummaryPart(summaryParts, "archived", threadMetadata["archived"]?.ToString());
                AddSummaryPart(summaryParts, "locked", threadMetadata["locked"]?.ToString());
            }

            return string.Join(" ", summaryParts);
        }

        private static void AddSummaryPart(ICollection<string> parts, string key, string value) {
            if (!string.IsNullOrWhiteSpace(value)) {
                parts.Add($"{key}={value}");
            }
        }
    }
}
