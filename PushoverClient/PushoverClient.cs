#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org> and contributors

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Utilities;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Core.Utility.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.PushoverClient {

    public class PushoverClient {

        // https://pushover.net/api
        private const string API_URL = "https://api.pushover.net/1/messages.json";

        public PushoverClient() {
            PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
            PushoverEmergRetryInterval = Properties.Settings.Default.PushoverEmergRetryInterval;
            PushoverEmergExpireAfter = Properties.Settings.Default.PushoverEmergExpireAfter;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task PushMessage(string title, string message, Priority priority, NotificationSound notificationSound, CancellationToken ct) {
            Logger.Debug("Pushing message");

            try {
                if (ct.IsCancellationRequested) {
                    Logger.Info("Push cancelled");
                    return;
                }

                var body = PushoverRequestArguments.CreateJSON(PushoverAppKey, PushoverUserKey, title, message, device: string.Empty, priority, DateTime.Now, notificationSound, PushoverEmergRetryInterval, PushoverEmergExpireAfter);
                var request = new HttpPostRequest(API_URL, body, "application/json");
                await request.Request(ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to Pushover: {ex.Message}");
                throw;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(PushoverAppKey) || string.IsNullOrWhiteSpace(PushoverAppKey)) {
                issues.Add("Pushover app key is missing");
            }

            if (string.IsNullOrEmpty(PushoverUserKey) || string.IsNullOrWhiteSpace(PushoverUserKey)) {
                issues.Add("Pushover user key is missing");
            }

            return issues;
        }

        private string PushoverAppKey { get; set; }
        private string PushoverUserKey { get; set; }
        private int PushoverEmergRetryInterval { get; set; }
        private int PushoverEmergExpireAfter { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverAppKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
                    break;

                case "PushoverUserKey":
                    PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
                    break;

                case "PushoverEmergRetryInterval":
                    PushoverEmergRetryInterval = Properties.Settings.Default.PushoverEmergRetryInterval;
                    break;

                case "PushoverEmergExpireAfter":
                    PushoverEmergExpireAfter = Properties.Settings.Default.PushoverEmergExpireAfter;
                    break;
            }
        }
    }

    [JsonObject]
    public class PushoverRequestArguments {

        public static string CreateJSON(string appKey, string userKey, string title, string message, string device, Priority priority, DateTime timestamp, NotificationSound notificationSound, int retry, int expire) {
            return JsonConvert.SerializeObject(PushoverRequestArguments.Create(appKey, userKey, title, message, device, priority, timestamp, notificationSound, retry, expire), Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        public static PushoverRequestArguments Create(string appKey, string userKey, string title, string message, string device, Priority priority, DateTime timestamp, NotificationSound notificationSound, int retry, int expire) {
            if (string.IsNullOrEmpty(userKey)) {
                throw new ArgumentException("User key must be supplied", nameof(userKey));
            }

            var time = new DateTimeOffset(timestamp).ToUnixTimeSeconds();
            var sound = notificationSound == NotificationSound.NotSet ? null : notificationSound.ToString().ToLower();

            var arguments = new PushoverRequestArguments(token: appKey, user: userKey, device: device, title: title, message: message, priority: (int)priority, timestamp: time, sound: sound, retry: retry, expire: expire);

            return arguments;
        }

        private PushoverRequestArguments(string token, string user, string device, string title, string message, int priority, long timestamp, string sound, int retry, int expire) {
            Token = token;
            User = user;
            Device = device;
            Title = title;
            Message = message;
            Priority = priority;
            Timestamp = timestamp;
            Sound = sound;

            // Emergency priority requires retry and expire values
            if (priority == 2) {
                Retry = retry;
                Expire = expire;
            }
        }

        [JsonProperty(PropertyName = "token")]
        public string Token { get; }

        [JsonProperty(PropertyName = "user")]
        public string User { get; }

        [JsonProperty(PropertyName = "device")]
        public string Device { get; }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; }

        [JsonProperty(PropertyName = "priority")]
        public int Priority { get; }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp { get; }

        [JsonProperty(PropertyName = "sound")]
        public string Sound { get; }

        [JsonProperty(PropertyName = "retry")]
        public int Retry { get; }

        [JsonProperty(PropertyName = "expire")]
        public int Expire { get; }
    }
}