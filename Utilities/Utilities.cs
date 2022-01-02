﻿#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Astrometry;
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

        internal static string ResolveTokens(string text, ISequenceItem sequenceItem = null, bool urlEncode = false) {
            DeepSkyObject target = null;

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

        internal static string ResolveFailureTokens(string text, ISequenceItem sequenceItem, bool urlEncode = false) {
            if (sequenceItem.Status == SequenceEntityStatus.FAILED) {
                var errorList = new List<string>() { };

                text = text.Replace(@"$$FAILED_ITEM$$", DoUrlEncode(urlEncode, sequenceItem.Name));
                text = text.Replace(@"$$FAILED_ATTEMPTS$$", sequenceItem.Attempts.ToString());

                text = !string.IsNullOrEmpty(sequenceItem.Parent?.Name.ToString())
                    ? text.Replace(@"$$FAILED_INSTR_SET$$", DoUrlEncode(urlEncode, sequenceItem.Parent.Name))
                    : text.Replace(@"$$FAILED_INSTR_SET$$", DoUrlEncode(urlEncode, "----"));

                if (sequenceItem is IValidatable validatableItem) {
                    errorList = validatableItem.Issues as List<string>;
                }

                text = errorList?.Count > 0
                    ? text.Replace(@"$$ERROR_LIST$$", DoUrlEncode(urlEncode, string.Join(", ", errorList)))
                    : text.Replace(@"$$ERROR_LIST$$", DoUrlEncode(urlEncode, string.Empty));
            }

            return text;
        }

        public static DeepSkyObject FindDsoInfo(ISequenceContainer container) {
            DeepSkyObject target = null;
            ISequenceContainer acontainer = container;

            while (acontainer != null) {
                if (acontainer is IDeepSkyObjectContainer dsoContainer) {
                    target = dsoContainer.Target.DeepSkyObject;
                    break;
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
    }
}