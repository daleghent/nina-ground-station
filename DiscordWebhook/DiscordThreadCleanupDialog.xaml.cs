#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    public partial class DiscordThreadCleanupDialog : Window {

        public DiscordThreadCleanupDialog() {
            InitializeComponent();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            LogTextBox.ScrollToEnd();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            (DataContext as DiscordThreadCleanupVM)?.RequestCancel();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            // Closing the window mid-run counts as a cancel; the cleanup stops at its next checkpoint.
            (DataContext as DiscordThreadCleanupVM)?.RequestCancel();
        }
    }
}
