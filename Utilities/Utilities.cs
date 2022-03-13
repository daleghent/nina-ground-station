#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal class Utilities {
        internal const string RuntimeErrorMessage = "An unspecified failure occurred while running this item. Refer to NINA's log for details.";
        internal const int cancelTimeout = 10; // in seconds

        internal static string ResolveTokens(string text, ISequenceItem sequenceItem = null, bool urlEncode = false) {
            IDeepSkyObject target = null;

            if (sequenceItem != null) {
                target = FindDsoInfo(sequenceItem.Parent);
            }

            var datetime = DateTime.Now;
            var datetimeUtc = DateTime.UtcNow;

            text = !string.IsNullOrEmpty(target?.Name)
                ? text.Replace(@"$$TARGET_NAME$$", DoUrlEncode(urlEncode, target.Name))
                : text.Replace(@"$$TARGET_NAME$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.RAString)
                ? text.Replace(@"$$TARGET_RA$$", DoUrlEncode(urlEncode, target.Coordinates.RAString))
                : text.Replace(@"$$TARGET_RA$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.DecString)
                ? text.Replace(@"$$TARGET_DEC$$", DoUrlEncode(urlEncode, target.Coordinates.DecString))
                : text.Replace(@"$$TARGET_DEC$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.RA.ToString())
                ? text.Replace(@"$$TARGET_RA_DECIMAL$$", DoUrlEncode(urlEncode, target.Coordinates.RA.ToString()))
                : text.Replace(@"$$TARGET_RA_DECIMAL$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.Dec.ToString())
                ? text.Replace(@"$$TARGET_DEC_DECIMAL$$", DoUrlEncode(urlEncode, target.Coordinates.Dec.ToString()))
                : text.Replace(@"$$TARGET_DEC_DECIMAL$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.Epoch.ToString())
                ? text.Replace(@"$$TARGET_EPOCH$$", DoUrlEncode(urlEncode, target.Coordinates.Epoch.ToString()))
                : text.Replace(@"$$TARGET_EPOCH$$", DoUrlEncode(urlEncode, "----"));

            text = text.Replace(@"$$INSTRUCTION_SET$$",
                string.IsNullOrEmpty(sequenceItem?.Parent?.Name) ? DoUrlEncode(urlEncode, "----") : DoUrlEncode(urlEncode, sequenceItem.Parent.Name));

            text = text.Replace(@"$$DATE$$", DoUrlEncode(urlEncode, datetime.ToString("d")));
            text = text.Replace(@"$$TIME$$", DoUrlEncode(urlEncode, datetime.ToString("T")));
            text = text.Replace(@"$$DATETIME$$", DoUrlEncode(urlEncode, datetime.ToString("G")));

            text = text.Replace(@"$$DATE_UTC$$", DoUrlEncode(urlEncode, datetimeUtc.ToString("d")));
            text = text.Replace(@"$$TIME_UTC$$", DoUrlEncode(urlEncode, datetimeUtc.ToString("T")));
            text = text.Replace(@"$$DATETIME_UTC$$", DoUrlEncode(urlEncode, datetimeUtc.ToString("G")));
            text = text.Replace(@"$$UNIX_EPOCH$$", UnixEpoch().ToString());

            text = ParseFormattedDateTime(text, urlEncode);

            text = text.Replace(@"$$SYSTEM_NAME$$", DoUrlEncode(urlEncode, Environment.MachineName));
            text = text.Replace(@"$$USER_NAME$$", DoUrlEncode(urlEncode, Environment.UserName));
            text = text.Replace(@"$$NINA_VERSION$$", DoUrlEncode(urlEncode, CoreUtil.Version));
            text = text.Replace(@"$$GS_VERSION$$", DoUrlEncode(urlEncode, GroundStation.GetVersion()));

            return text;
        }

        internal static string ResolveFailureTokens(string text, FailedItem failedItem, bool urlEncode = false) {
            text = text.Replace(@"$$FAILED_ITEM$$", DoUrlEncode(urlEncode, failedItem.Name));
            text = text.Replace(@"$$FAILED_ITEM_DESC$$", DoUrlEncode(urlEncode, failedItem.Description));
            text = text.Replace(@"$$FAILED_ITEM_CATEGORY$$", DoUrlEncode(urlEncode, failedItem.Category));
            text = text.Replace(@"$$FAILED_ATTEMPTS$$", failedItem.Attempts.ToString());

            text = !string.IsNullOrEmpty(failedItem.ParentName)
                ? text.Replace(@"$$FAILED_INSTR_SET$$", DoUrlEncode(urlEncode, failedItem.ParentName))
                : text.Replace(@"$$FAILED_INSTR_SET$$", DoUrlEncode(urlEncode, "----"));

            var reasonList = new List<string>();

            if (failedItem.Reasons.Count > 0) {
                foreach (var reason in failedItem.Reasons) {
                    reasonList.Add(reason.Reason);
                }
            } else {
                reasonList.Add(string.Empty);
            }

            text = text.Replace(@"$$ERROR_LIST$$", DoUrlEncode(urlEncode, string.Join(", ", reasonList)));

            return text;
        }

        public static IDeepSkyObject FindDsoInfo(ISequenceContainer container) {
            IDeepSkyObject target = null;
            ISequenceContainer acontainer = container;

            while (acontainer != null) {
                if (acontainer is IDeepSkyObjectContainer dsoContainer) {
                    if (dsoContainer.Target.DeepSkyObject != null) {
                        target = dsoContainer.Target.DeepSkyObject;
                        break;
                    }
                }

                acontainer = acontainer.Parent;
            }

            return target;
        }

        internal static long UnixEpoch() {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private static string ParseFormattedDateTime(string text, bool urlEncode) {
            string pattern = @"\$\$FORMAT_DATETIME(?<isUTC>_UTC)?\s+(?<specifier>.*)\$\$";

            foreach (Match dateTimeMatch in Regex.Matches(text, pattern)) {
                var dateRegex = new Regex(Regex.Escape(dateTimeMatch.Value));

                try {
                    text = dateTimeMatch.Groups["isUTC"].Success
                        ? dateRegex.Replace(text, DoUrlEncode(urlEncode, DateTime.UtcNow.ToString(dateTimeMatch.Groups["specifier"].Value)))
                        : dateRegex.Replace(text, DoUrlEncode(urlEncode, DateTime.Now.ToString(dateTimeMatch.Groups["specifier"].Value)));
                } catch {
                    text = dateRegex.Replace(text, DoUrlEncode(urlEncode, "[Invalid DateTime format]"));
                }
            }

            return text;
        }

        private static string DoUrlEncode(bool doUrlEncode, string text) {
            return doUrlEncode ? HttpUtility.UrlEncode(text) : text;
        }

        private static FailedItem GetFailedItem(ISequenceItem sequenceItem) {
            var failedItem = new FailedItem();

            if (sequenceItem.Status == SequenceEntityStatus.FAILED) {
                failedItem.Name = sequenceItem.Name;
                failedItem.ParentName = sequenceItem.Parent.Name;
                failedItem.Attempts = sequenceItem.Attempts;
                failedItem.Description = sequenceItem.Description;
                failedItem.Category = sequenceItem.Category;

                if (sequenceItem is IValidatable validatableItem && validatableItem.Issues.Count > 0) {
                    foreach (var issue in validatableItem.Issues) {
                        var failureReason = new FailureReason {
                            Reason = string.IsNullOrEmpty(issue) ? RuntimeErrorMessage : issue,
                        };

                        failedItem.Reasons.Add(failureReason);
                    }
                } else {
                    var failureReason = new FailureReason {
                        Reason = RuntimeErrorMessage,
                    };

                    failedItem.Reasons.Add(failureReason);
                }

                Logger.Debug($"Failed item: {failedItem.Name}, Reason count: {failedItem.Reasons.Count}");
            }

            return failedItem;
        }

        public static List<FailedItem> GetFailedItems(ISequenceItem sequenceItem) {
            var failedItems = new List<FailedItem>();

            if (sequenceItem is ISequenceContainer sequenceContainer && sequenceContainer is ParallelContainer) {
                var sequenceItems = sequenceContainer.GetItemsSnapshot();

                Logger.Debug($"Found a ParallelContainer with {sequenceItems.Count} items in it");

                foreach (SequenceItem item in sequenceItems) {
                    if (item.Status == SequenceEntityStatus.FAILED) {
                        var failedItem = GetFailedItem(item);

                        if (!string.IsNullOrEmpty(failedItem.Name)) {
                            failedItems.Add(failedItem);
                        }
                    }
                }
            } else if (sequenceItem.Status == SequenceEntityStatus.FAILED) {
                var failedItem = GetFailedItem(sequenceItem);

                if (!string.IsNullOrEmpty(failedItem.Name)) {
                    failedItems.Add(failedItem);
                }
            }

            return failedItems;
        }
    }
}