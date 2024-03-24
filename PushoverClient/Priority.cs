#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

namespace DaleGhent.NINA.GroundStation.PushoverClient {

    /// <summary>
    /// https://pushover.net/api#priority
    /// </summary>
    public enum Priority {
        Lowest = -2,
        Low = -1,
        Normal = 0,
        High = 1,
        Emergency = 2
    }
}