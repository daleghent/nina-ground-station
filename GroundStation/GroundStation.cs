#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

namespace DaleGhent.NINA.GroundStation {
    [Export(typeof(IPluginManifest))]
    public class GroundStation : PluginBase, ISettings, INotifyPropertyChanged {

        [ImportingConstructor]
        public GroundStation() {
            if (Properties.Settings.Default.UpgradeSettings) {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeSettings = false;
                Properties.Settings.Default.Save();
            }
        }

        public string IFTTTWebhookKey {
            get {
                return Properties.Settings.Default.IFTTTWebhookKey;
            }
            set {
                Properties.Settings.Default.IFTTTWebhookKey = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string PushoverAppKey {
            get {
                return Properties.Settings.Default.PushoverAppKey;
            }
            set {
                Properties.Settings.Default.PushoverAppKey = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string PushoverUserKey {
            get {
                return Properties.Settings.Default.PushoverUserKey;
            }
            set {
                Properties.Settings.Default.PushoverUserKey = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpFromAddress {
            get {
                return Properties.Settings.Default.SmtpFromAddress;
            }
            set {
                Properties.Settings.Default.SmtpFromAddress = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpDefaultRecipients {
            get {
                return Properties.Settings.Default.SmtpDefaultRecipients;
            }
            set {
                Properties.Settings.Default.SmtpDefaultRecipients = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpHostName {
            get {
                return Properties.Settings.Default.SmtpHostName;
            }
            set {
                Properties.Settings.Default.SmtpHostName = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public ushort SmtpHostPort {
            get {
                return Properties.Settings.Default.SmtpHostPort;
            }
            set {
                Properties.Settings.Default.SmtpHostPort = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpUsername {
            get {
                return Properties.Settings.Default.SmtpUsername;
            }
            set {
                Properties.Settings.Default.SmtpUsername = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpPassword {
            get {
                return Properties.Settings.Default.SmtpPassword;
            }
            set {
                Properties.Settings.Default.SmtpPassword = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}