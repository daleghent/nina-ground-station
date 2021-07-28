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
using PushoverClient;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
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
                return Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);
            }
            set {
                Properties.Settings.Default.IFTTTWebhookKey = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string PushoverAppKey {
            get {
                return Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            }
            set {
                Properties.Settings.Default.PushoverAppKey = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string PushoverUserKey {
            get {
                return Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
            }
            set {
                Properties.Settings.Default.PushoverUserKey = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public Priority[] PushoverPriorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().Where(p => p != Priority.Emergency).ToArray();

        public NotificationSound[] PushoverNotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

        public NotificationSound PushoverDefaultNotificationSound {
            get => Properties.Settings.Default.PushoverDefaultNotificationSound;
            set {
                Properties.Settings.Default.PushoverDefaultNotificationSound = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultNotificationPriority {
            get => Properties.Settings.Default.PushoverDefaultNotificationPriority;
            set {
                Properties.Settings.Default.PushoverDefaultNotificationPriority = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public NotificationSound PushoverDefaultFailureSound {
            get => Properties.Settings.Default.PushoverDefaultFailureSound;
            set {
                Properties.Settings.Default.PushoverDefaultFailureSound = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public Priority PushoverDefaultFailurePriority {
            get => Properties.Settings.Default.PushoverDefaultFailurePriority;
            set {
                Properties.Settings.Default.PushoverDefaultFailurePriority = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpFromAddress {
            get {
                return Properties.Settings.Default.SmtpFromAddress;
            }
            set {
                Properties.Settings.Default.SmtpFromAddress = value.Trim();
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpDefaultRecipients {
            get {
                return Properties.Settings.Default.SmtpDefaultRecipients;
            }
            set {
                Properties.Settings.Default.SmtpDefaultRecipients = value.Trim();
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpHostName {
            get {
                return Properties.Settings.Default.SmtpHostName;
            }
            set {
                Properties.Settings.Default.SmtpHostName = value.Trim();
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
                return Security.Decrypt(Properties.Settings.Default.SmtpUsername);
            }
            set {
                Properties.Settings.Default.SmtpUsername = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string SmtpPassword {
            get {
                return Security.Decrypt(Properties.Settings.Default.SmtpPassword);
            }
            set {
                Properties.Settings.Default.SmtpPassword = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string TelegramAccessToken {
            get {
                return Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
            }
            set {
                Properties.Settings.Default.TelegramAccessToken = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public string TelegramChatId {
            get {
                return Security.Decrypt(Properties.Settings.Default.TelegramChatId);
            }
            set {
                Properties.Settings.Default.TelegramChatId = Security.Encrypt(value.Trim());
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public static string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}