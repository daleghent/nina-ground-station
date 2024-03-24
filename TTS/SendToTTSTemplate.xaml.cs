#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DaleGhent.NINA.GroundStation.TTS {

    /// <summary>
    /// Interaction logic for SendToTTSTemplate.xaml
    /// </summary>
    [Export(typeof(ResourceDictionary))]
    public partial class SendToTTSTemplate : ResourceDictionary {

        public SendToTTSTemplate() {
            InitializeComponent();
        }
    }
}