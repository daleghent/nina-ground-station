#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DaleGhent.NINA.GroundStation {

    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}