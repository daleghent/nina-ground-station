#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Equipment.MyDome;
using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Equipment.Equipment.MyFlatDevice;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyGuider;
using NINA.Equipment.Equipment.MyRotator;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Equipment.Equipment.MySwitch;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Equipment.MyWeatherData;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DaleGhent.NINA.GroundStation.MetadataClient {

    public interface IMetadata {
        CameraInfo CameraInfo { get; }
        DomeInfo DomeInfo { get; }
        FilterWheelInfo FilterWheelInfo { get; }
        FlatDeviceInfo FlatDeviceInfo { get; }
        FocuserInfo FocuserInfo { get; }
        GuiderInfo GuiderInfo { get; }
        RotatorInfo RotatorInfo { get; }
        SafetyMonitorInfo SafetyMonitorInfo { get; }
        SwitchInfo SwitchInfo { get; }
        TelescopeInfo TelescopeInfo { get; }
        WeatherDataInfo WeatherDataInfo { get; }
        CultureInfo CultureInfo { get; set; }
    }
}