#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using System.Diagnostics;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Collections.Generic;
using DaleGhent.NINA.GroundStation.Slack;

namespace DaleGhent.NINA.GroundStation.Config {

    public partial class GroundStationConfigDataTemplate : ResourceDictionary {

        public GroundStationConfigDataTemplate() {
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
                if (elem.DataContext is GroundStationConfig) {
                    GroundStation.GroundStationConfig.SetSmtpPassword(elem.SecurePassword);
                }
            }
        }

        private void PasswordBox_Smtp_Loaded(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    elem.Password = GroundStation.GroundStationConfig.SmtpPassword;
                }
            }
        }

        private void PasswordBox_Mqtt_PasswordChanged(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    GroundStation.GroundStationConfig.SetMqttPassword(elem.SecurePassword);
                }
            }
        }

        private void PasswordBox_Mqtt_Loaded(object sender, RoutedEventArgs e) {
            if (sender is PasswordBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    elem.Password = GroundStation.GroundStationConfig.MqttPassword;
                }
            }
        }

        private void DiscordImageTypeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    List<string> typeNames = [];

                    foreach (string type in elem.SelectedItems) {
                        typeNames.Add(type);
                    }

                    var stringList = string.Join(",", typeNames);
                    GroundStation.GroundStationConfig.DiscordImageTypesSelected = stringList;
                }
            }
        }

        private void DiscordImageTypeListBox_Initialized(object sender, EventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    var selectedTypes = GroundStation.GroundStationConfig.DiscordImageTypesSelected.Split(',');
                    elem.SelectedItems.Clear();

                    foreach (var imgType in selectedTypes) {
                        elem.SelectedItems.Add(imgType);
                    }
                }
            }
        }

        private void SlackChannelsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    if (elem.SelectedItem is Channel channel) {
                        GroundStation.GroundStationConfig.SlackSelectedChannelId = channel.Id;
                        GroundStation.GroundStationConfig.SlackSelectedChannelIsPrivate = channel.IsPrivate;
                        GroundStation.GroundStationConfig.SlackSelectedChannelCreateDate = channel.CreateDate.ToString("r");
                        GroundStation.GroundStationConfig.SlackSelectedChannelNumMembers = channel.NumMembers;
                        GroundStation.GroundStationConfig.SlackShowChannelInfo = true;
                    }
                }
            }
        }

        private void SlackImageTypeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    List<string> typeNames = [];

                    foreach (string type in elem.SelectedItems) {
                        typeNames.Add(type);
                    }

                    var stringList = string.Join(",", typeNames);
                    GroundStation.GroundStationConfig.SlackImageTypesSelected = stringList;
                }
            }
        }

        private void SlackImageTypeListBox_Initialized(object sender, EventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    var selectedTypes = GroundStation.GroundStationConfig.SlackImageTypesSelected.Split(',');
                    elem.SelectedItems.Clear();

                    foreach (var imgType in selectedTypes) {
                        elem.SelectedItems.Add(imgType);
                    }
                }
            }
        }

        private void MqttImageTypeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    List<string> typeNames = [];

                    foreach (string type in elem.SelectedItems) {
                        typeNames.Add(type);
                    }

                    var stringList = string.Join(",", typeNames);
                    GroundStation.GroundStationConfig.MqttImageTypesSelected = stringList;
                }
            }
        }

        private void MqttImageTypeListBox_Initialized(object sender, EventArgs e) {
            if (sender is ListBox elem) {
                if (elem.DataContext is GroundStationConfig) {
                    var selectedTypes = GroundStation.GroundStationConfig.MqttImageTypesSelected.Split(',');
                    elem.SelectedItems.Clear();

                    foreach (var imgType in selectedTypes) {
                        elem.SelectedItems.Add(imgType);
                    }
                }
            }
        }
    }
}