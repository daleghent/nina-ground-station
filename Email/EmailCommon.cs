#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Email {
    public class EmailCommon {

        public EmailCommon() {
            SmtpHostName = Properties.Settings.Default.SmtpHostName;
            SmtpHostPort = Properties.Settings.Default.SmtpHostPort;
            SmtpUsername = Security.Decrypt(Properties.Settings.Default.SmtpUsername);
            SmtpPassword = Security.Decrypt(Properties.Settings.Default.SmtpPassword);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task SendEmail(MimeMessage message, CancellationToken ct) {
            var xMailerHeader = new Header("X-Mailer", $"Ground Station/{GroundStation.GetVersion()}, NINA/{CoreUtil.Version}");
            message.Headers.Add(xMailerHeader);

            var smtp = new SmtpClient();

            try {
                await smtp.ConnectAsync(SmtpHostName, SmtpHostPort, SecureSocketOptions.Auto, ct);

                if (!string.IsNullOrEmpty(SmtpUsername) && !string.IsNullOrEmpty(SmtpPassword)) {
                    await smtp.AuthenticateAsync(SmtpUsername, SmtpPassword, ct);
                }

                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);
            } catch (SocketException ex) {
                Logger.Error($"SmtpEmail: Connection to {SmtpHostName}:{SmtpHostPort} failed: {ex.SocketErrorCode}: {ex.Message}");
                throw ex;
            } catch (AuthenticationException ex) {
                Logger.Error($"SendEmail: User {SmtpUsername} failed to authenticate with {SmtpHostName}:{SmtpHostPort}");
                throw ex;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(SmtpHostName) || string.IsNullOrWhiteSpace(SmtpHostName)) {
                issues.Add("SMTP server is not configured");
            }

            if (SmtpHostPort < 1) {
                issues.Add("SMTP port is invalid");
            }

            return issues;
        }

        private string SmtpHostName { get; set; }
        private ushort SmtpHostPort { get; set; }
        private string SmtpUsername { get; set; }
        private string SmtpPassword { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "SmtpHostName":
                    SmtpHostName = Properties.Settings.Default.SmtpHostName;
                    break;
                case "SmtpHostPort":
                    SmtpHostPort = Properties.Settings.Default.SmtpHostPort;
                    break;
                case "SmtpUsername":
                    SmtpUsername = Security.Decrypt(Properties.Settings.Default.SmtpUsername);
                    break;
                case "SmtpPassword":
                    SmtpPassword = Security.Decrypt(Properties.Settings.Default.SmtpPassword);
                    break;
            }
        }
    }
}
