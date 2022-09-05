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
using NINA.Equipment.Interfaces.Mediator;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.MetadataClient {
    public class Metadata : IMetadata {
        private readonly ICameraMediator cameraMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly ISwitchMediator switchMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IWeatherDataMediator weatherDataMediator;

        public Metadata(ICameraMediator cameraMediator,
                            IDomeMediator domeMediator,
                            IFilterWheelMediator filterWheelMediator,
                            IFlatDeviceMediator flatDeviceMediator,
                            IFocuserMediator focuserMediator,
                            IGuiderMediator guiderMediator,
                            IRotatorMediator rotatorMediator,
                            ISafetyMonitorMediator safetyMonitorMediator,
                            ISwitchMediator switchMediator,
                            ITelescopeMediator telescopeMediator,
                            IWeatherDataMediator weatherDataMediator) {
            this.cameraMediator = cameraMediator;
            this.domeMediator = domeMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.rotatorMediator = rotatorMediator;
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.switchMediator = switchMediator;
            this.telescopeMediator = telescopeMediator;
            this.weatherDataMediator = weatherDataMediator;
        }

        public CameraInfo CameraInfo => cameraMediator.GetInfo();
        public DomeInfo DomeInfo => domeMediator.GetInfo();
        public FilterWheelInfo FilterWheelInfo => filterWheelMediator.GetInfo();
        public FlatDeviceInfo FlatDeviceInfo => flatDeviceMediator.GetInfo();
        public FocuserInfo FocuserInfo => focuserMediator.GetInfo();
        public GuiderInfo GuiderInfo => guiderMediator.GetInfo();
        public RotatorInfo RotatorInfo => rotatorMediator.GetInfo();
        public SafetyMonitorInfo SafetyMonitorInfo => safetyMonitorMediator.GetInfo();
        public SwitchInfo SwitchInfo => switchMediator.GetInfo();
        public TelescopeInfo TelescopeInfo => telescopeMediator.GetInfo();
        public WeatherDataInfo WeatherDataInfo => weatherDataMediator.GetInfo();
        public CultureInfo CultureInfo { get; set; } = CultureInfo.InvariantCulture;
    }
}
