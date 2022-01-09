#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    public class FailedItem {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public int Attempts { get; set; } = 0;
        public List<FailureReason> Reasons { get; set; } = new List<FailureReason>();
    }

    public class FailureReason {
        public string Reason { get; set; } = string.Empty;
    }
}