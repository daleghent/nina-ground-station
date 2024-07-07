#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using System;

namespace DaleGhent.NINA.GroundStation.Slack {

    [JsonObject(MemberSerialization.OptIn)]
    public class Channel {
        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public DateTime CreateDate { get; set; }

        [JsonProperty]
        public bool IsPrivate { get; set; }

        [JsonProperty]
        public int NumMembers { get; set; }

        public override bool Equals(object obj) {
            return obj is Channel other && this.Id == other.Id;
        }
    }
}
