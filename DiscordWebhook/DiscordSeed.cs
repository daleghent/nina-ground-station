#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    internal static class DiscordSeed {
        private const string FailureSeedPrefix = "❌ ";
        private const string SeedMarkerPrefix = "\u2060\u2063";

        public static bool HasSeedMarker(string messageContent) {
            return !string.IsNullOrEmpty(messageContent)
                && messageContent.StartsWith(SeedMarkerPrefix, StringComparison.Ordinal);
        }

        public static bool IsOwnedWebhook(string messageWebhookId, string configuredWebhookId) {
            return !string.IsNullOrWhiteSpace(messageWebhookId)
                && !string.IsNullOrWhiteSpace(configuredWebhookId)
                && string.Equals(messageWebhookId, configuredWebhookId, StringComparison.Ordinal);
        }

        public static bool IsDirectChannelPost(string messageWebhookId, string configuredWebhookId, string messageContent) {
            return IsOwnedWebhook(messageWebhookId, configuredWebhookId) && !HasSeedMarker(messageContent);
        }

        public static bool SeedTextMatchesThreadName(string seedText, string threadName) {
            var normalizedSeedText = NormalizeSeedText(seedText);
            if (string.IsNullOrWhiteSpace(normalizedSeedText) || string.IsNullOrWhiteSpace(threadName)) {
                return false;
            }

            return string.Equals(normalizedSeedText, threadName, StringComparison.Ordinal)
                || string.Equals(normalizedSeedText, $"{FailureSeedPrefix}{threadName}", StringComparison.Ordinal);
        }

        public static bool IsFailedSeedText(string seedText) {
            var normalizedSeedText = NormalizeSeedText(seedText);
            return !string.IsNullOrWhiteSpace(normalizedSeedText)
                && normalizedSeedText.StartsWith(FailureSeedPrefix, StringComparison.Ordinal);
        }

        public static string BuildSeedText(string threadName) {
            return $"{SeedMarkerPrefix}{threadName}";
        }

        public static string BuildFailedSeedText(string threadName) {
            return $"{SeedMarkerPrefix}{FailureSeedPrefix}{threadName}";
        }

        public static string NormalizeSeedText(string seedText) {
            if (string.IsNullOrWhiteSpace(seedText)) {
                return seedText;
            }

            return seedText.StartsWith(SeedMarkerPrefix, StringComparison.Ordinal)
                ? seedText.Substring(SeedMarkerPrefix.Length)
                : seedText;
        }
    }
}
