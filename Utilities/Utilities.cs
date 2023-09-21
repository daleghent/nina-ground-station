#region "copyright"

/*
    Copyright 2021-2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal partial class Utilities {
        internal const string RuntimeErrorMessage = "An unspecified failure occurred while running this item. Refer to NINA's log for details.";
        internal const int cancelTimeout = 10; // in seconds

        internal static string ResolveTokens(string text, ISequenceEntity sequenceItem = null, IMetadata metadata = null, bool urlEncode = false) {
            IDeepSkyObject target = null;
            CultureInfo culture = CultureInfo.InvariantCulture;

            if (metadata != null) {
                culture = metadata.CultureInfo;
            }

            if (sequenceItem != null) {
                target = FindDsoInfo(sequenceItem.Parent);
            }

            var datetime = DateTime.Now;
            var datetimeUtc = datetime.ToUniversalTime();

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
                ? text.Replace(@"$$TARGET_RA_DECIMAL$$", DoUrlEncode(urlEncode, target.Coordinates.RA.ToString("F3", culture)))
                : text.Replace(@"$$TARGET_RA_DECIMAL$$", DoUrlEncode(urlEncode, "----"));

            text = !string.IsNullOrEmpty(target?.Coordinates.Dec.ToString())
                ? text.Replace(@"$$TARGET_DEC_DECIMAL$$", DoUrlEncode(urlEncode, target.Coordinates.Dec.ToString("F3", culture)))
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

            text = ParseFormattedDateTime(text, datetime, urlEncode);

            text = text.Replace(@"$$SYSTEM_NAME$$", DoUrlEncode(urlEncode, Environment.MachineName));
            text = text.Replace(@"$$USER_NAME$$", DoUrlEncode(urlEncode, Environment.UserName));
            text = text.Replace(@"$$NINA_VERSION$$", DoUrlEncode(urlEncode, CoreUtil.Version));
            text = text.Replace(@"$$GS_VERSION$$", DoUrlEncode(urlEncode, GroundStation.GetVersion()));

            if (metadata == null) {
                return text;
            }

            var camera = metadata.CameraInfo;
            var dome = metadata.DomeInfo;
            var fwheel = metadata.FilterWheelInfo;
            var flat = metadata.FlatDeviceInfo;
            var focuser = metadata.FocuserInfo;
            var rotator = metadata.RotatorInfo;
            var safety = metadata.SafetyMonitorInfo;
            var mount = metadata.TelescopeInfo;
            var weather = metadata.WeatherDataInfo;

            // Camera info
            if (camera.Connected) {
                text = text.Replace(@"$$CAMERA_NAME$$", DoUrlEncode(urlEncode, camera.Name));

                text = text.Replace(@"$$CAMERA_BATTERY$$", camera.Battery > -1 ?
                        DoUrlEncode(urlEncode, camera.Battery.ToString("F", culture)) :
                        DoUrlEncode(urlEncode, "--"));

                text = text.Replace(@"$$CAMERA_SENSOR_TEMP$$", double.IsNaN(camera.Temperature) ?
                        DoUrlEncode(urlEncode, "--") :
                        DoUrlEncode(urlEncode, camera.Temperature.ToString("F", culture)));
            } else {
                var pattern = CameraRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Dome info
            if (dome.Connected) {
                text = text.Replace(@"$$DOME_NAME$$", DoUrlEncode(urlEncode, dome.Name));

                text = text.Replace(@"$$DOME_IS_PARKED$$", DoUrlEncode(urlEncode, dome.AtPark.ToString()));

                text = text.Replace(@"$$DOME_IS_HOME$$", DoUrlEncode(urlEncode, dome.AtHome.ToString()));

                text = text.Replace(@"$$DOME_SHUTTER$$", DoUrlEncode(urlEncode, dome.ShutterStatus.ToString()));

                text = text.Replace(@"$$DOME_AZ_DECIMAL$$", DoUrlEncode(urlEncode, dome.Azimuth.ToString("F3", culture)));
            } else {
                var pattern = DomeRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Filter wheel info
            if (fwheel.Connected) {
                text = text.Replace(@"$$FWHEEL_NAME$$", DoUrlEncode(urlEncode, fwheel.Name));

                text = text.Replace(@"$$FWHEEL_FILTER_NAME$$", string.IsNullOrEmpty(fwheel.SelectedFilter?.Name ?? string.Empty) ?
                        DoUrlEncode(urlEncode, "----") :
                        DoUrlEncode(urlEncode, fwheel.SelectedFilter.Name));

                text = text.Replace(@"$$FWHEEL_FILTER_POS$$", (fwheel.SelectedFilter?.Position ?? -1) < 0 ?
                        DoUrlEncode(urlEncode, "--") :
                        DoUrlEncode(urlEncode, fwheel.SelectedFilter.Position.ToString(culture)));
            } else {
                var pattern = FWheelRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Flat device info
            if (flat.Connected) {
                text = text.Replace(@"$$FLAT_NAME$$", DoUrlEncode(urlEncode, flat.Name));

                text = text.Replace(@"$$FLAT_COVER_STATUS$$", DoUrlEncode(urlEncode, flat.LocalizedCoverState));

                text = text.Replace(@"$$FLAT_LAMP_STATUS$$", DoUrlEncode(urlEncode, flat.LocalizedLightOnState));
            } else {
                var pattern = FlatDeviceRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Focuser info
            if (focuser.Connected) {
                text = text.Replace(@"$$FOCUSER_NAME$$", DoUrlEncode(urlEncode, focuser.Name));

                text = text.Replace(@"$$FOCUSER_POSITION$$", DoUrlEncode(urlEncode, focuser.Position.ToString(culture)));

                text = text.Replace(@"$$FOCUSER_TEMP$$", double.IsNaN(focuser.Temperature) ?
                        DoUrlEncode(urlEncode, "--") :
                        DoUrlEncode(urlEncode, focuser.Temperature.ToString("F", culture)));
            } else {
                var pattern = FocuserRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Rotator info
            if (rotator.Connected) {
                text = text.Replace(@"$$ROTATOR_NAME$$", DoUrlEncode(urlEncode, rotator.Name));

                text = text.Replace(@"$$ROTATOR_ANGLE$$", DoUrlEncode(urlEncode, rotator.MechanicalPosition.ToString("F", culture)));
            } else {
                var pattern = RotatorRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Safety monitor info
            if (safety.Connected) {
                text = text.Replace(@"$$SAFETY_NAME$$", DoUrlEncode(urlEncode, safety.Name));

                text = text.Replace(@"$$SAFETY_IS_SAFE$$", DoUrlEncode(urlEncode, safety.IsSafe.ToString()));
            } else {
                var pattern = SafetyRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Mount info
            if (mount.Connected) {
                text = text.Replace(@"$$MOUNT_NAME$$", DoUrlEncode(urlEncode, mount.Name));

                text = text.Replace(@"$$MOUNT_POINTING_STATE$$", DoUrlEncode(urlEncode, mount.SideOfPier.ToString()));

                text = text.Replace(@"$$MOUNT_RA$$", DoUrlEncode(urlEncode, mount.Coordinates.RAString));

                text = text.Replace(@"$$MOUNT_RA_DECIMAL$$", DoUrlEncode(urlEncode, mount.Coordinates.RA.ToString("F3", culture)));

                text = text.Replace(@"$$MOUNT_DEC$$", DoUrlEncode(urlEncode, mount.Coordinates.DecString));

                text = text.Replace(@"$$MOUNT_DEC_DECIMAL$$", DoUrlEncode(urlEncode, mount.Coordinates.Dec.ToString("F3", culture)));

                text = text.Replace(@"$$MOUNT_IS_PARKED$$", DoUrlEncode(urlEncode, mount.AtPark.ToString()));

                text = text.Replace(@"$$MOUNT_IS_HOME$$", DoUrlEncode(urlEncode, mount.AtHome.ToString()));

                text = text.Replace(@"$$MOUNT_ALT$$", DoUrlEncode(urlEncode, mount.AltitudeString));

                text = text.Replace(@"$$MOUNT_ALT_DECIMAL$$", DoUrlEncode(urlEncode, mount.Altitude.ToString("F3", culture)));

                text = text.Replace(@"$$MOUNT_AZ$$", DoUrlEncode(urlEncode, mount.AzimuthString));

                text = text.Replace(@"$$MOUNT_AZ_DECIMAL$$", DoUrlEncode(urlEncode, mount.Azimuth.ToString("F3", culture)));

                text = text.Replace(@"$$MOUNT_TTF$$", DoUrlEncode(urlEncode, mount.TimeToMeridianFlipString));
            } else {
                var pattern = MountRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

            // Weather info
            if (weather.Connected) {
                text = text.Replace(@"$$WX_NAME$$", DoUrlEncode(urlEncode, weather.Name));

                text = text.Replace(@"$$WX_CLOUD_COVER$$", double.IsNaN(weather.CloudCover) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.CloudCover.ToString("F", culture)));

                text = text.Replace(@"$$WX_DEWPOINT$$", double.IsNaN(weather.DewPoint) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.DewPoint.ToString("F", culture)));

                text = text.Replace(@"$$WX_HUMIDITY$$", double.IsNaN(weather.Humidity) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.Humidity.ToString("F", culture)));

                text = text.Replace(@"$$WX_PRESSURE$$", double.IsNaN(weather.Pressure) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.Pressure.ToString("F", culture)));

                text = text.Replace(@"$$WX_RAINRATE$$", double.IsNaN(weather.RainRate) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.RainRate.ToString("F", culture)));

                text = text.Replace(@"$$WX_SKYBRT$$", double.IsNaN(weather.SkyBrightness) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.SkyBrightness.ToString("F", culture)));

                text = text.Replace(@"$$WX_SKYQUAL$$", double.IsNaN(weather.SkyQuality) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.SkyQuality.ToString("F", culture)));

                text = text.Replace(@"$$WX_SKYTEMP$$", double.IsNaN(weather.SkyTemperature) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.SkyTemperature.ToString("F", culture)));

                text = text.Replace(@"$$WX_STARFWHM$$", double.IsNaN(weather.StarFWHM) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.StarFWHM.ToString("F", culture)));

                text = text.Replace(@"$$WX_AMBTEMP$$", double.IsNaN(weather.Temperature) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.Temperature.ToString("F", culture)));

                text = text.Replace(@"$$WX_WINDDIR$$", double.IsNaN(weather.WindDirection) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.WindDirection.ToString("F", culture)));

                text = text.Replace(@"$$WX_WINDGUST$$", double.IsNaN(weather.WindGust) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.WindGust.ToString("F", culture)));

                text = text.Replace(@"$$WX_WINDSPD$$", double.IsNaN(weather.WindSpeed) ?
                    DoUrlEncode(urlEncode, "--") :
                    DoUrlEncode(urlEncode, weather.WindSpeed.ToString("F", culture)));
            } else {
                var pattern = WeatherRegex();
                text = pattern.Replace(text, DoUrlEncode(urlEncode, "----"));
            }

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
            return (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
        }

        private static string ParseFormattedDateTime(string text, DateTime datetime, bool urlEncode) {
            string pattern = @"\${2}FORMAT_DATETIME(?<isUTC>_UTC)?\s+(?<specifier>.*?)\${2}";

            foreach (Match dateTimeMatch in Regex.Matches(text, pattern).Cast<Match>()) {
                var dateRegex = new Regex(Regex.Escape(dateTimeMatch.Value));

                try {
                    text = dateTimeMatch.Groups["isUTC"].Success
                        ? dateRegex.Replace(text, DoUrlEncode(urlEncode, datetime.ToUniversalTime().ToString(dateTimeMatch.Groups["specifier"].Value)))
                        : dateRegex.Replace(text, DoUrlEncode(urlEncode, datetime.ToString(dateTimeMatch.Groups["specifier"].Value)));
                } catch {
                    text = dateRegex.Replace(text, DoUrlEncode(urlEncode, "[Invalid DateTime format]"));
                }
            }

            return text;
        }

        internal static string DoUrlEncode(bool doUrlEncode, string text) {
            return doUrlEncode ? HttpUtils.UrlTokenEncode(Encoding.Unicode.GetBytes(text)) : text;
        }

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
