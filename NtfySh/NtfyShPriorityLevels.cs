#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Utilities;
using System.ComponentModel;

namespace DaleGhent.NINA.GroundStation.NtfySh {

    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum NtfyShPriorityLevels {

        [Description("Default")]
        Default,

        [Description("High")]
        High,

        [Description("Low")]
        Low,

        [Description("Max")]
        Max,

        [Description("Min")]
        Min,
    }
}
