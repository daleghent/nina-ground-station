#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The catalogue of Ground Station's own <c>$$TOKEN$$</c> message tokens. Every token that the
    /// plugin substitutes is defined here exactly once, which makes the same set available both to
    /// message resolution in <see cref="Utilities"/> and to autocompletion in the expression text
    /// box.
    /// </summary>
    /// <remarks>
    /// Tokens marked as deprecated have a N.I.N.A. 3.3 symbol equivalent and remain supported for
    /// compatibility; new messages should prefer the <c>{Symbol}</c> expression syntax, which is
    /// expanded separately by <see cref="ExpressionUtilities"/>. Tokens without such a marker have
    /// no symbol equivalent and remain the only way to obtain that value.
    /// </remarks>
    internal static partial class GsTokenRegistry {
        private const string Unavailable = "----";
        private const string NoValue = "--";

        private static readonly List<GsToken> tokens = [.. GeneralTokens(), .. DeviceTokens(), .. FailureTokens(), .. DynamicTokens()];

        /// <summary>
        /// All known tokens, ordered by name.
        /// </summary>
        internal static IReadOnlyList<GsToken> All { get; } = [.. tokens.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)];

        /// <summary>
        /// All known tokens keyed by their bare name, without the <c>$$</c> delimiters.
        /// </summary>
        internal static IReadOnlyDictionary<string, GsToken> Tokens { get; } =
            tokens.ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Looks up a token by its bare name.
        /// </summary>
        internal static bool TryGet(string name, out GsToken token) => Tokens.TryGetValue(name, out token);

        private static IEnumerable<GsToken> GeneralTokens() {
            yield return new GsToken("TARGET_NAME", TokenGroup.Target, "The name of the target the instruction belongs to",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Name) ? ctx.Target.Name : Unavailable)) {
                IsDeprecated = true,
                ReplacementHint = "{NINA_TargetName}",
            };

            yield return new GsToken("TARGET_RA", TokenGroup.Target, "The target's right ascension, formatted as sexagesimal",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Coordinates.RAString) ? ctx.Target.Coordinates.RAString : Unavailable));

            yield return new GsToken("TARGET_DEC", TokenGroup.Target, "The target's declination, formatted as sexagesimal",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Coordinates.DecString) ? ctx.Target.Coordinates.DecString : Unavailable));

            yield return new GsToken("TARGET_RA_DECIMAL", TokenGroup.Target, "The target's right ascension in decimal hours",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Coordinates.RA.ToString())
                    ? ctx.Target.Coordinates.RA.ToString("F3", ctx.Culture)
                    : Unavailable)) {
                IsDeprecated = true,
                ReplacementHint = "{NINA_TargetRAJ2000}, which is unformatted",
            };

            yield return new GsToken("TARGET_DEC_DECIMAL", TokenGroup.Target, "The target's declination in decimal degrees",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Coordinates.Dec.ToString())
                    ? ctx.Target.Coordinates.Dec.ToString("F3", ctx.Culture)
                    : Unavailable)) {
                IsDeprecated = true,
                ReplacementHint = "{NINA_TargetDecJ2000}, which is unformatted",
            };

            yield return new GsToken("TARGET_EPOCH", TokenGroup.Target, "The epoch of the target's coordinates",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.Target?.Coordinates.Epoch.ToString())
                    ? ctx.Target.Coordinates.Epoch.ToString()
                    : Unavailable));

            yield return new GsToken("INSTRUCTION_SET", TokenGroup.General, "The name of the instruction set that contains the instruction",
                ctx => ctx.Encode(string.IsNullOrEmpty(ctx.SequenceItem?.Parent?.Name) ? Unavailable : ctx.SequenceItem.Parent.Name));

            yield return new GsToken("DATE", TokenGroup.General, "The current local date",
                ctx => ctx.Encode(ctx.Now.ToString("d")));

            yield return new GsToken("TIME", TokenGroup.General, "The current local time",
                ctx => ctx.Encode(ctx.Now.ToString("T")));

            yield return new GsToken("DATETIME", TokenGroup.General, "The current local date and time",
                ctx => ctx.Encode(ctx.Now.ToString("G")));

            yield return new GsToken("DATE_UTC", TokenGroup.General, "The current UTC date",
                ctx => ctx.Encode(ctx.NowUtc.ToString("d")));

            yield return new GsToken("TIME_UTC", TokenGroup.General, "The current UTC time",
                ctx => ctx.Encode(ctx.NowUtc.ToString("T")));

            yield return new GsToken("DATETIME_UTC", TokenGroup.General, "The current UTC date and time",
                ctx => ctx.Encode(ctx.NowUtc.ToString("G")));

            // Emitted without URL encoding as the value is always numeric
            yield return new GsToken("UNIX_EPOCH", TokenGroup.General, "The current time as seconds since the Unix epoch",
                ctx => Utilities.UnixEpoch(ctx.Now).ToString());

            yield return new GsToken("SYSTEM_NAME", TokenGroup.General, "The name of this computer",
                ctx => ctx.Encode(Environment.MachineName));

            yield return new GsToken("USER_NAME", TokenGroup.General, "The name of the user running N.I.N.A.",
                ctx => ctx.Encode(Environment.UserName));

            yield return new GsToken("NINA_VERSION", TokenGroup.General, "The running N.I.N.A. version",
                ctx => ctx.Encode(CoreUtil.Version));

            yield return new GsToken("GS_VERSION", TokenGroup.General, "The running Ground Station version",
                ctx => ctx.Encode(GroundStation.GetVersion()));
        }

        private static IEnumerable<GsToken> FailureTokens() {
            yield return new GsToken("FAILED_ITEM", TokenGroup.Failure, "The name of the instruction that failed",
                ctx => ctx.Encode(ctx.FailedItem.Name));

            yield return new GsToken("FAILED_ITEM_DESC", TokenGroup.Failure, "The description of the instruction that failed",
                ctx => ctx.Encode(ctx.FailedItem.Description));

            yield return new GsToken("FAILED_ITEM_CATEGORY", TokenGroup.Failure, "The category of the instruction that failed",
                ctx => ctx.Encode(ctx.FailedItem.Category));

            // Emitted without URL encoding as the value is always numeric
            yield return new GsToken("FAILED_ATTEMPTS", TokenGroup.Failure, "The number of attempts that were made",
                ctx => ctx.FailedItem.Attempts.ToString());

            yield return new GsToken("FAILED_INSTR_SET", TokenGroup.Failure, "The instruction set that contained the failed instruction",
                ctx => ctx.Encode(!string.IsNullOrEmpty(ctx.FailedItem.ParentName) ? ctx.FailedItem.ParentName : Unavailable));

            yield return new GsToken("ERROR_LIST", TokenGroup.Failure, "The list of reasons the instruction failed",
                ctx => ctx.Encode(string.Join(", ", FailureReasons(ctx.FailedItem))));
        }

        private static IEnumerable<string> FailureReasons(FailedItem failedItem) {
            if (failedItem.Reasons.Count == 0) {
                return [string.Empty];
            }

            return failedItem.Reasons.Select(reason => reason.Reason);
        }
    }
}
