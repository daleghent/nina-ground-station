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
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DaleGhent.NINA.GroundStation {

    [Export(typeof(ResourceDictionary))]
    public partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            var procStartInfo = new ProcessStartInfo() {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            };

            _ = Process.Start(procStartInfo);
            e.Handled = true;
        }

        private void PasswordBox_Smtp_PasswordChanged(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStation vm) {
                    vm.SetSmtpPassword(elem.SecurePassword);
                }
            }
        }

        private void PasswordBox_Smtp_Loaded(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStation vm) {
                    elem.Password = vm.SmtpPassword;
                }
            }
        }

        private void PasswordBox_Mqtt_PasswordChanged(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStation vm) {
                    vm.SetMqttPassword(elem.SecurePassword);
                }
            }
        }

        private void PasswordBox_Mqtt_Loaded(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStation vm) {
                    elem.Password = vm.MqttPassword;
                }
            }
        }
    }
}