#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The tokens that take an inline argument and so cannot be resolved by name lookup alone.
    /// They are expanded by <see cref="ExpandDynamicTokens"/> before the name sweep runs.
    /// </summary>
    internal static partial class GsTokenRegistry {
        private const string InvalidDateTimeFormat = "[Invalid DateTime format]";
        private const string SampleDateTimeSpecifier = "yyyy-MM-dd HH:mm:ss";

        private static IEnumerable<GsToken> DynamicTokens() {
            yield return new GsToken("FORMAT_DATETIME", TokenGroup.General,
                $"The current local date and time using a custom format specifier, eg. $$FORMAT_DATETIME {SampleDateTimeSpecifier}$$",
                _ => string.Empty) {
                IsDynamic = true,
                CompletionTemplate = $"FORMAT_DATETIME {SampleDateTimeSpecifier}",
                CompletionCaretOffset = "FORMAT_DATETIME ".Length,
            };

            yield return new GsToken("FORMAT_DATETIME_UTC", TokenGroup.General,
                $"The current UTC date and time using a custom format specifier, eg. $$FORMAT_DATETIME_UTC {SampleDateTimeSpecifier}$$",
                _ => string.Empty) {
                IsDynamic = true,
                CompletionTemplate = $"FORMAT_DATETIME_UTC {SampleDateTimeSpecifier}",
                CompletionCaretOffset = "FORMAT_DATETIME_UTC ".Length,
            };
        }

        /// <summary>
        /// Expands every dynamic token found in <paramref name="text"/>. An unusable format
        /// specifier yields <c>[Invalid DateTime format]</c> rather than failing the whole message.
        /// </summary>
        internal static string ExpandDynamicTokens(string text, TokenContext context) {
            foreach (Match match in FormatDateTimeRegex().Matches(text).Cast<Match>()) {
                var matchRegex = new Regex(Regex.Escape(match.Value));
                var datetime = match.Groups["isUTC"].Success ? context.NowUtc : context.Now;

                string value;

                try {
                    value = datetime.ToString(match.Groups["specifier"].Value);
                } catch {
                    value = InvalidDateTimeFormat;
                }

                text = matchRegex.Replace(text, context.Encode(value));
            }

            return text;
        }

        [GeneratedRegex(@"\${2}FORMAT_DATETIME(?<isUTC>_UTC)?\s+(?<specifier>.*?)\${2}", RegexOptions.Compiled)]
        private static partial Regex FormatDateTimeRegex();
    }
}
