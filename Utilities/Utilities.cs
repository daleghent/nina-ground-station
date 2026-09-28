#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using NINA.Astrometry.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal partial class Utilities {
        internal const string RuntimeErrorMessage = "An unspecified failure occurred while running this item. Refer to NINA's log for details.";
        internal const int cancelTimeout = 10; // in seconds

        /// <summary>
        /// Resolves Ground Station's $$TOKEN$$ message tokens. The tokens themselves, along with the
        /// values they yield, are defined in <see cref="GsTokenRegistry"/>. Tokens belonging to a
        /// device that is not connected are collapsed to "----" as a family, which also covers token
        /// names that no longer exist. Symbol expressions using the {Symbol} syntax are expanded
        /// separately by <see cref="ExpressionUtilities"/> after this method returns.
        /// </summary>
        internal static string ResolveTokens(string text, ISequenceEntity sequenceItem = null, IMetadata metadata = null, bool urlEncode = false) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            var context = new TokenContext(sequenceItem, metadata, null, urlEncode);

            text = GsTokenRegistry.ExpandDynamicTokens(text, context);
            text = ReplaceTokens(text, context, group => group != TokenGroup.Failure && IsGroupConnected(group, metadata));

            if (metadata == null) {
                return text;
            }

            // Any token belonging to a disconnected device is still in the text at this point. Blank
            // out each such family wholesale so that no unresolved token is ever emitted.
            var unavailable = DoUrlEncode(urlEncode, "----");

            if (!metadata.CameraInfo.Connected) { text = CameraRegex().Replace(text, unavailable); }
            if (!metadata.DomeInfo.Connected) { text = DomeRegex().Replace(text, unavailable); }
            if (!metadata.FilterWheelInfo.Connected) { text = FWheelRegex().Replace(text, unavailable); }
            if (!metadata.FlatDeviceInfo.Connected) { text = FlatDeviceRegex().Replace(text, unavailable); }
            if (!metadata.FocuserInfo.Connected) { text = FocuserRegex().Replace(text, unavailable); }
            if (!metadata.RotatorInfo.Connected) { text = RotatorRegex().Replace(text, unavailable); }
            if (!metadata.SafetyMonitorInfo.Connected) { text = SafetyRegex().Replace(text, unavailable); }
            if (!metadata.TelescopeInfo.Connected) { text = MountRegex().Replace(text, unavailable); }
            if (!metadata.WeatherDataInfo.Connected) { text = WeatherRegex().Replace(text, unavailable); }

            return text;
        }

        /// <summary>
        /// Resolves the $$TOKEN$$ message tokens that describe a failed instruction.
        /// </summary>
        internal static string ResolveFailureTokens(string text, FailedItem failedItem, bool urlEncode = false) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }

            var context = new TokenContext(null, null, failedItem, urlEncode);

            return ReplaceTokens(text, context, group => group == TokenGroup.Failure);
        }

        /// <summary>
        /// Substitutes every known token whose group satisfies <paramref name="groupFilter"/>. Tokens
        /// that are unknown, dynamic, or belong to an excluded group are left in place.
        /// </summary>
        private static string ReplaceTokens(string text, TokenContext context, Func<TokenGroup, bool> groupFilter) {
            if (!text.Contains("$$")) {
                return text;
            }

            return TokenRegex().Replace(text, match => {
                var name = match.Groups["name"].Value;

                return GsTokenRegistry.TryGet(name, out var token) && !token.IsDynamic && groupFilter(token.Group)
                    ? token.Resolve(context)
                    : match.Value;
            });
        }

        /// <summary>
        /// Whether the device backing <paramref name="group"/> is connected and can therefore supply
        /// a value. Groups that are not backed by a device are always resolvable.
        /// </summary>
        private static bool IsGroupConnected(TokenGroup group, IMetadata metadata) {
            if (metadata == null) {
                return group is TokenGroup.General or TokenGroup.Target;
            }

            return group switch {
                TokenGroup.Camera => metadata.CameraInfo.Connected,
                TokenGroup.Dome => metadata.DomeInfo.Connected,
                TokenGroup.FilterWheel => metadata.FilterWheelInfo.Connected,
                TokenGroup.FlatDevice => metadata.FlatDeviceInfo.Connected,
                TokenGroup.Focuser => metadata.FocuserInfo.Connected,
                TokenGroup.Mount => metadata.TelescopeInfo.Connected,
                TokenGroup.Rotator => metadata.RotatorInfo.Connected,
                TokenGroup.Safety => metadata.SafetyMonitorInfo.Connected,
                TokenGroup.Weather => metadata.WeatherDataInfo.Connected,
                _ => true,
            };
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

        internal static long UnixEpoch(DateTime dateTime) {
            return (long)dateTime.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds;
        }

        internal static string DoUrlEncode(bool doUrlEncode, string text) {
            return doUrlEncode ? WebUtility.UrlEncode(text) : text;
        }

        [GeneratedRegex(@"\${2}(?<name>[A-Z0-9_]+)\${2}", RegexOptions.Compiled)]
        private static partial Regex TokenRegex();

        [GeneratedRegex(@"\${2}CAMERA_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex CameraRegex();

        [GeneratedRegex(@"\${2}DOME_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex DomeRegex();

        [GeneratedRegex(@"\${2}FWHEEL_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex FWheelRegex();

        [GeneratedRegex(@"\${2}FLAT_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex FlatDeviceRegex();

        [GeneratedRegex(@"\${2}FOCUSER_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex FocuserRegex();

        [GeneratedRegex(@"\${2}MOUNT_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex MountRegex();

        [GeneratedRegex(@"\${2}ROTATOR_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex RotatorRegex();

        [GeneratedRegex(@"\${2}SAFETY_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex SafetyRegex();

        [GeneratedRegex(@"\${2}WX_[A-Z0-9_]+\${2}", RegexOptions.Compiled)]
        private static partial Regex WeatherRegex();
    }
}
