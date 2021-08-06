#region "copyright"

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

namespace DaleGhent.NINA.GroundStation {

    public class Utilities {

        public static string ResolveTokens(string text, ISequenceItem sequenceItem) {
            var target = FindDsoInfo(sequenceItem.Parent);
            var datetime = DateTime.Now;
            var datetimeUtc = DateTime.UtcNow;

            text = !string.IsNullOrEmpty(target?.Name)
                ? text.Replace(@"$$TARGET_NAME$$", target.Name)
                : text.Replace(@"$$TARGET_NAME$$", "----");

            text = !string.IsNullOrEmpty(target?.Coordinates.RAString)
                ? text.Replace(@"$$TARGET_RA$$", target.Coordinates.RAString)
                : text.Replace(@"$$TARGET_RA$$", "----");

            text = !string.IsNullOrEmpty(target?.Coordinates.DecString)
                ? text.Replace(@"$$TARGET_DEC$$", target.Coordinates.DecString)
                : text.Replace(@"$$TARGET_DEC$$", "----");

            text = !string.IsNullOrEmpty(target?.Coordinates.RA.ToString())
                ? text.Replace(@"$$TARGET_RA_DECIMAL$$", target.Coordinates.RA.ToString())
                : text.Replace(@"$$TARGET_RA_DECIMAL$$", "----");

            text = !string.IsNullOrEmpty(target?.Coordinates.Dec.ToString())
                ? text.Replace(@"$$TARGET_DEC_DECIMAL$$", target.Coordinates.Dec.ToString())
                : text.Replace(@"$$TARGET_DEC_DECIMAL$$", "----");

            text = !string.IsNullOrEmpty(target?.Coordinates.Epoch.ToString())
                ? text.Replace(@"$$TARGET_EPOCH$$", target.Coordinates.Epoch.ToString())
                : text.Replace(@"$$TARGET_EPOCH$$", "----");

            text = text.Replace(@"$$INSTRUCTION_SET$$", sequenceItem.Parent.Name);

            text = text.Replace(@"$$DATE$$", datetime.ToString("d"));
            text = text.Replace(@"$$TIME$$", datetime.ToString("T"));
            text = text.Replace(@"$$DATETIME$$", datetime.ToString("G"));

            text = text.Replace(@"$$DATE_UTC$$", datetimeUtc.ToString("d"));
            text = text.Replace(@"$$TIME_UTC$$", datetimeUtc.ToString("T"));
            text = text.Replace(@"$$DATETIME_UTC$$", datetimeUtc.ToString("G"));

            text = ParseFormattedDateTime(text);

            text = text.Replace(@"$$SYSTEM_NAME$$", Environment.MachineName);
            text = text.Replace(@"$$USER_NAME$$", Environment.UserName);
            text = text.Replace(@"$$NINA_VERSION$$", CoreUtil.Version);
            text = text.Replace(@"$$GS_VERSION$$", GroundStation.GetVersion());

            return text;
        }

        public static string ResolveFailureTokens(string text, ISequenceItem sequenceItem) {
            if (sequenceItem.Status == SequenceEntityStatus.FAILED) {
                var errorList = new List<string>();

                text = text.Replace(@"$$FAILED_ITEM$$", sequenceItem.Name);
                text = text.Replace(@"$$FAILED_ATTEMPTS$$", sequenceItem.Attempts.ToString());
                text = text.Replace(@"$$FAILED_INSTR_SET$$", sequenceItem.Parent.Name);

                if (sequenceItem is IValidatable validatableItem) {
                    errorList = validatableItem.Issues as List<string>;
                }

                text = text.Replace(@"$$ERROR_LIST$$", string.Join(", ", errorList));
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

        private static string ParseFormattedDateTime(string text) {
            string pattern = @"\$\$FORMAT_DATETIME(?<isUTC>_UTC)?\s+(?<specifier>.*)\$\$";

            foreach (Match dateTimeMatch in Regex.Matches(text, pattern)) {
                var dateRegex = new Regex(Regex.Escape(dateTimeMatch.Value));

                try {
                    text = dateTimeMatch.Groups["isUTC"].Success
                        ? dateRegex.Replace(text, DateTime.UtcNow.ToString(dateTimeMatch.Groups["specifier"].Value))
                        : dateRegex.Replace(text, DateTime.Now.ToString(dateTimeMatch.Groups["specifier"].Value));
                } catch {
                    text = dateRegex.Replace(text, "[Invalid DateTime format]");
                }
            }

            return text;
        }
    }
}