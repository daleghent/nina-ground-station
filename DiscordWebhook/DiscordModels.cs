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

    public sealed class DiscordApiException : Exception {
        public DiscordApiException(string message, Exception innerException = null) : base(message, innerException) {
        }
    }

    /// <summary>
    /// Progress snapshot for old-thread cleanup. While the channel scan is running <see cref="TotalCount"/>
    /// is 0 and only <see cref="LogLine"/> is meaningful; once deletion starts, counts and the time
    /// estimate are populated.
    /// </summary>
    public sealed record ThreadCleanupProgress(string LogLine, int CompletedCount, int TotalCount, TimeSpan? EstimatedTimeRemaining);

    internal sealed record WebhookMetadata(string Url, string WebhookId, string ChannelId, string GuildId);

    internal sealed record OldSessionThreadCandidate(string ThreadId, string MessageId, string DisplayName, DateTimeOffset CreatedAt);

    internal sealed record BotManagedThreadResolution(string ThreadId, bool CanEditFailureSeed);

    internal sealed record ReusableStarterMessage(string MessageId, string ThreadId, bool CanEditFailureSeed, string[] DuplicateMessageIdsToDelete);

    internal sealed record VisibleParentEntry(string MessageId, string SeedText, string ThreadId, int Order, bool IsWebhookOwned);
}
