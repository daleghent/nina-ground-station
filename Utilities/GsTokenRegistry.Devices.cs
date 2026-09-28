#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.Collections.Generic;

namespace DaleGhent.NINA.GroundStation.Utilities {

    /// <summary>
    /// The equipment tokens. Each resolver assumes its device is connected; a disconnected device
    /// causes the whole family to be blanked out before these resolvers are consulted.
    /// </summary>
    internal static partial class GsTokenRegistry {

        private static IEnumerable<GsToken> DeviceTokens() {
            // Camera
            yield return new GsToken("CAMERA_NAME", TokenGroup.Camera, "The name of the connected camera",
                ctx => ctx.Encode(ctx.Metadata.CameraInfo.Name));

            yield return new GsToken("CAMERA_BATTERY", TokenGroup.Camera, "The camera's battery level",
                ctx => ctx.Encode(ctx.Metadata.CameraInfo.Battery > -1
                    ? ctx.Metadata.CameraInfo.Battery.ToString("F", ctx.Culture)
                    : NoValue));

            yield return new GsToken("CAMERA_SENSOR_TEMP", TokenGroup.Camera, "The camera's sensor temperature",
                ctx => ctx.Encode(double.IsNaN(ctx.Metadata.CameraInfo.Temperature)
                    ? NoValue
                    : ctx.Metadata.CameraInfo.Temperature.ToString("F", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Camera_Temperature}, which is unformatted",
            };

            // Dome
            yield return new GsToken("DOME_NAME", TokenGroup.Dome, "The name of the connected dome",
                ctx => ctx.Encode(ctx.Metadata.DomeInfo.Name));

            yield return new GsToken("DOME_IS_PARKED", TokenGroup.Dome, "Whether the dome is parked",
                ctx => ctx.Encode(ctx.Metadata.DomeInfo.AtPark.ToString()));

            yield return new GsToken("DOME_IS_HOME", TokenGroup.Dome, "Whether the dome is at its home position",
                ctx => ctx.Encode(ctx.Metadata.DomeInfo.AtHome.ToString()));

            yield return new GsToken("DOME_SHUTTER", TokenGroup.Dome, "The readable state of the dome's shutter",
                ctx => ctx.Encode(ctx.Metadata.DomeInfo.ShutterStatus.ToString())) {
                ReplacementHint = "Retained: {Dome_ShutterStatus} yields a numeric constant, not this readable name",
            };

            yield return new GsToken("DOME_AZ_DECIMAL", TokenGroup.Dome, "The dome's azimuth in decimal degrees",
                ctx => ctx.Encode(ctx.Metadata.DomeInfo.Azimuth.ToString("F3", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Dome_Azimuth}, which is unformatted",
            };

            // Filter wheel
            yield return new GsToken("FWHEEL_NAME", TokenGroup.FilterWheel, "The name of the connected filter wheel",
                ctx => ctx.Encode(ctx.Metadata.FilterWheelInfo.Name));

            yield return new GsToken("FWHEEL_FILTER_NAME", TokenGroup.FilterWheel, "The name of the selected filter",
                ctx => ctx.Encode(string.IsNullOrEmpty(ctx.Metadata.FilterWheelInfo.SelectedFilter?.Name ?? string.Empty)
                    ? Unavailable
                    : ctx.Metadata.FilterWheelInfo.SelectedFilter.Name));

            yield return new GsToken("FWHEEL_FILTER_POS", TokenGroup.FilterWheel, "The position of the selected filter",
                ctx => ctx.Encode((ctx.Metadata.FilterWheelInfo.SelectedFilter?.Position ?? -1) < 0
                    ? NoValue
                    : ctx.Metadata.FilterWheelInfo.SelectedFilter.Position.ToString(ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{FilterWheel_CurrentFilterIndex}",
            };

            // Flat device
            yield return new GsToken("FLAT_NAME", TokenGroup.FlatDevice, "The name of the connected flat panel",
                ctx => ctx.Encode(ctx.Metadata.FlatDeviceInfo.Name));

            yield return new GsToken("FLAT_COVER_STATUS", TokenGroup.FlatDevice, "The localized state of the flat panel's cover",
                ctx => ctx.Encode(ctx.Metadata.FlatDeviceInfo.LocalizedCoverState)) {
                ReplacementHint = "Retained: {FlatPanel_CoverState} yields a numeric constant, not this localized text",
            };

            yield return new GsToken("FLAT_LAMP_STATUS", TokenGroup.FlatDevice, "The localized state of the flat panel's lamp",
                ctx => ctx.Encode(ctx.Metadata.FlatDeviceInfo.LocalizedLightOnState)) {
                ReplacementHint = "Retained: {FlatPanel_LightOn} yields a boolean, not this localized text",
            };

            // Focuser
            yield return new GsToken("FOCUSER_NAME", TokenGroup.Focuser, "The name of the connected focuser",
                ctx => ctx.Encode(ctx.Metadata.FocuserInfo.Name));

            yield return new GsToken("FOCUSER_POSITION", TokenGroup.Focuser, "The focuser's position",
                ctx => ctx.Encode(ctx.Metadata.FocuserInfo.Position.ToString(ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Focuser_Position}",
            };

            yield return new GsToken("FOCUSER_TEMP", TokenGroup.Focuser, "The focuser's temperature",
                ctx => ctx.Encode(double.IsNaN(ctx.Metadata.FocuserInfo.Temperature)
                    ? NoValue
                    : ctx.Metadata.FocuserInfo.Temperature.ToString("F", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Focuser_Temperature}, which is unformatted",
            };

            // Rotator
            yield return new GsToken("ROTATOR_NAME", TokenGroup.Rotator, "The name of the connected rotator",
                ctx => ctx.Encode(ctx.Metadata.RotatorInfo.Name));

            yield return new GsToken("ROTATOR_ANGLE", TokenGroup.Rotator, "The rotator's mechanical angle",
                ctx => ctx.Encode(ctx.Metadata.RotatorInfo.MechanicalPosition.ToString("F", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Rotator_MechanicalPosition}, which is unformatted",
            };

            yield return new GsToken("ROTATOR_SKY_ANGLE", TokenGroup.Rotator, "The rotator's sky angle",
                ctx => ctx.Encode(ctx.Metadata.RotatorInfo.Position.ToString("F", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Rotator_Position}, which is unformatted",
            };

            yield return new GsToken("ROTATOR_IS_SYNCED", TokenGroup.Rotator, "Whether the rotator is synced",
                ctx => ctx.Encode(ctx.Metadata.RotatorInfo.Synced.ToString()));

            // Safety monitor
            yield return new GsToken("SAFETY_NAME", TokenGroup.Safety, "The name of the connected safety monitor",
                ctx => ctx.Encode(ctx.Metadata.SafetyMonitorInfo.Name));

            yield return new GsToken("SAFETY_IS_SAFE", TokenGroup.Safety, "Whether conditions are reported as safe",
                ctx => ctx.Encode(ctx.Metadata.SafetyMonitorInfo.IsSafe.ToString())) {
                IsDeprecated = true,
                ReplacementHint = "{Safety_IsSafe}",
            };

            // Mount
            yield return new GsToken("MOUNT_NAME", TokenGroup.Mount, "The name of the connected mount",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Name));

            yield return new GsToken("MOUNT_POINTING_STATE", TokenGroup.Mount, "The mount's readable pointing state",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.SideOfPier.ToString())) {
                ReplacementHint = "Retained: {Mount_SideOfPier} yields a numeric constant, not this readable name",
            };

            yield return new GsToken("MOUNT_RA", TokenGroup.Mount, "The mount's right ascension, formatted as sexagesimal",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Coordinates.RAString));

            yield return new GsToken("MOUNT_RA_DECIMAL", TokenGroup.Mount, "The mount's right ascension in decimal hours, in its native epoch",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Coordinates.RA.ToString("F3", ctx.Culture))) {
                ReplacementHint = "Not equivalent to {Mount_RightAscensionJ2000}: that symbol is transformed to J2000, whereas this token reports the mount's native epoch",
            };

            yield return new GsToken("MOUNT_DEC", TokenGroup.Mount, "The mount's declination, formatted as sexagesimal",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Coordinates.DecString));

            yield return new GsToken("MOUNT_DEC_DECIMAL", TokenGroup.Mount, "The mount's declination in decimal degrees, in its native epoch",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Coordinates.Dec.ToString("F3", ctx.Culture))) {
                ReplacementHint = "Not equivalent to {Mount_DeclinationJ2000}: that symbol is transformed to J2000, whereas this token reports the mount's native epoch",
            };

            yield return new GsToken("MOUNT_IS_PARKED", TokenGroup.Mount, "Whether the mount is parked",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.AtPark.ToString())) {
                IsDeprecated = true,
                ReplacementHint = "{Mount_AtPark}",
            };

            yield return new GsToken("MOUNT_IS_HOME", TokenGroup.Mount, "Whether the mount is at its home position",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.AtHome.ToString()));

            yield return new GsToken("MOUNT_ALT", TokenGroup.Mount, "The mount's altitude, formatted as sexagesimal",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.AltitudeString));

            yield return new GsToken("MOUNT_ALT_DECIMAL", TokenGroup.Mount, "The mount's altitude in decimal degrees",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Altitude.ToString("F3", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Mount_Altitude}, which is unformatted",
            };

            yield return new GsToken("MOUNT_AZ", TokenGroup.Mount, "The mount's azimuth, formatted as sexagesimal",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.AzimuthString));

            yield return new GsToken("MOUNT_AZ_DECIMAL", TokenGroup.Mount, "The mount's azimuth in decimal degrees",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.Azimuth.ToString("F3", ctx.Culture))) {
                IsDeprecated = true,
                ReplacementHint = "{Mount_Azimuth}, which is unformatted",
            };

            yield return new GsToken("MOUNT_TTF", TokenGroup.Mount, "The time remaining until the meridian flip",
                ctx => ctx.Encode(ctx.Metadata.TelescopeInfo.TimeToMeridianFlipString));

            // Weather. Every $$WX_*$$ token except $$WX_NAME$$ has an unformatted {Weather_*} symbol
            // equivalent, eg. $$WX_AMBTEMP$$ -> {Weather_Temperature}
            yield return new GsToken("WX_NAME", TokenGroup.Weather, "The name of the connected weather source",
                ctx => ctx.Encode(ctx.Metadata.WeatherDataInfo.Name));

            yield return new GsToken("WX_CLOUD_COVER", TokenGroup.Weather, "The cloud cover percentage",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.CloudCover, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_CloudCover}, which is unformatted",
            };

            yield return new GsToken("WX_DEWPOINT", TokenGroup.Weather, "The dew point temperature",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.DewPoint, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_DewPoint}, which is unformatted",
            };

            yield return new GsToken("WX_HUMIDITY", TokenGroup.Weather, "The relative humidity",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.Humidity, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_Humidity}, which is unformatted",
            };

            yield return new GsToken("WX_PRESSURE", TokenGroup.Weather, "The barometric pressure",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.Pressure, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_Pressure}, which is unformatted",
            };

            yield return new GsToken("WX_RAINRATE", TokenGroup.Weather, "The rain rate",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.RainRate, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_RainRate}, which is unformatted",
            };

            yield return new GsToken("WX_SKYBRT", TokenGroup.Weather, "The sky brightness",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.SkyBrightness, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_SkyBrightness}, which is unformatted",
            };

            yield return new GsToken("WX_SKYQUAL", TokenGroup.Weather, "The sky quality",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.SkyQuality, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_SkyQuality}, which is unformatted",
            };

            yield return new GsToken("WX_SKYTEMP", TokenGroup.Weather, "The sky temperature",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.SkyTemperature, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_SkyTemperature}, which is unformatted",
            };

            yield return new GsToken("WX_STARFWHM", TokenGroup.Weather, "The measured star FWHM",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.StarFWHM, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_StarFWHM}, which is unformatted",
            };

            yield return new GsToken("WX_AMBTEMP", TokenGroup.Weather, "The ambient temperature",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.Temperature, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_Temperature}, which is unformatted",
            };

            yield return new GsToken("WX_WINDDIR", TokenGroup.Weather, "The wind direction",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.WindDirection, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_WindDirection}, which is unformatted",
            };

            yield return new GsToken("WX_WINDGUST", TokenGroup.Weather, "The wind gust speed",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.WindGust, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_WindGust}, which is unformatted",
            };

            yield return new GsToken("WX_WINDSPD", TokenGroup.Weather, "The wind speed",
                ctx => ctx.Encode(FormatWeather(ctx.Metadata.WeatherDataInfo.WindSpeed, ctx))) {
                IsDeprecated = true,
                ReplacementHint = "{Weather_WindSpeed}, which is unformatted",
            };
        }

        private static string FormatWeather(double value, TokenContext ctx)
            => double.IsNaN(value) ? NoValue : value.ToString("F", ctx.Culture);
    }
}
