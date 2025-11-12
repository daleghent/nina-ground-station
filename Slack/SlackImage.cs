#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.IO;

namespace DaleGhent.NINA.GroundStation.Slack {
    public class SlackImage {
        public string Title { get; set; }
        public Channel Channel { get; set; }
        public string BodyText { get; set; }
        public string FileName { get; set; }
        public Stream FileContent { get; set; } = null;
        public string FileType { get; set; }
    }
}
