#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Utilities;
using Google;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Email {

    public class EmailCommon {

        public EmailCommon() {
            EmailSystem = Properties.Settings.Default.EmailSystem;
            GoogleAccountName = Properties.Settings.Default.GoogleAccountName;
            SmtpHostName = Properties.Settings.Default.SmtpHostName;
            SmtpHostPort = Properties.Settings.Default.SmtpHostPort;
            SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
            SmtpUsername = Security.Decrypt(Properties.Settings.Default.SmtpUsername);
            SmtpPassword = Security.Decrypt(Properties.Settings.Default.SmtpPassword);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task SendMessage(MimeMessage message, CancellationToken ct) {
            if (EmailSystem == 0) {
                message.From.Add(MailboxAddress.Parse(SmtpFromAddress));
            }

            var xMailerHeader = new Header("X-Mailer", $"Ground Station/{GroundStation.GetVersion()}, NINA/{CoreUtil.Version}");
            message.Headers.Add(xMailerHeader);

            Logger.Info($"Ground Station: Sending email via {Lists.EmailSystemList[EmailSystem]} to {message.To}");

            try {
                switch (EmailSystem) {
                    case 0:
                        await SendSmtp(message, ct);
                        break;

                    case 1:
                        await SendGmail(message, ct);
                        break;
                }
            } catch (Exception ex) {
                var errorMsg = ex.Message;

                if (ex.InnerException != null) {
                    errorMsg += $"{Environment.NewLine}Error detail:{ex.InnerException.Message}";
                }

                Notification.ShowError($"Failed to send email:{Environment.NewLine}{errorMsg}");
                throw;
            }
        }

        private async Task SendSmtp(MimeMessage message, CancellationToken ct) {
            var smtp = new SmtpClient();

            try {
                await smtp.ConnectAsync(SmtpHostName, SmtpHostPort, SecureSocketOptions.Auto, ct);
                await smtp.AuthenticateAsync(SmtpUsername, SmtpPassword, ct);

                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);
            } catch (SocketException ex) {
                Logger.Error($"Connection to {SmtpHostName}:{SmtpHostPort} failed: {ex.SocketErrorCode}: {ex.Message}");
                throw;
            } catch (AuthenticationException ex) {
                Logger.Error($"User {SmtpUsername} failed to authenticate with {SmtpHostName}:{SmtpHostPort}: {ex.Message}");
                throw;
            }
        }

        private async Task SendGmail(MimeMessage message, CancellationToken ct) {
            var msgStream = new MemoryStream();
            await message.WriteToAsync(msgStream, ct);
            msgStream.Position = 0;

            var sr = new StreamReader(msgStream);
            byte[] rawMessageBytes = Encoding.UTF8.GetBytes(sr.ReadToEnd());

            var gmailMessage = new Google.Apis.Gmail.v1.Data.Message {
                Raw = Convert.ToBase64String(rawMessageBytes)
            };

            // Gmail appears to rate-limit email send API calls. Wait a little bit if we've sent an email in the past 3 seconds.
            if ((DateTime.Now - LastEmailSentTimestamp).TotalSeconds < 3d) {
                await Task.Delay(TimeSpan.FromSeconds(4), ct);
            }

            try {
                LastEmailSentTimestamp = DateTime.Now;
                var gmailService = GoogleOauth2.GetGmailService(GoogleAccountName);
                var result = await gmailService.Users.Messages.Send(gmailMessage, GoogleAccountName).ExecuteAsync();
            } catch (GoogleApiException ex) {
                Logger.Error($"Failed to send to Gmail API: {ex.Message}");
                throw;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (EmailSystem == 0) {
                // SMTP-only stuff

                if (string.IsNullOrEmpty(SmtpHostName) || string.IsNullOrWhiteSpace(SmtpHostName)) {
                    issues.Add("SMTP server is not configured");
                } else {
                    try {
                        if (!IPAddress.TryParse(SmtpHostName, out var smtpIpAddr)) {
                            var ipHostEntry = Dns.GetHostEntry(SmtpHostName);
                        }
                    } catch (SocketException) {
                        issues.Add($"Unable to resolve {SmtpHostName}");
                    } catch { }
                }

                if (SmtpHostPort < 1) {
                    issues.Add("SMTP port is invalid");
                }

                if (string.IsNullOrEmpty(SmtpFromAddress) || string.IsNullOrWhiteSpace(SmtpFromAddress)) {
                    issues.Add("Email from address is missing");
                }
            } else if (EmailSystem == 1) {
                // Gmail-only stuff

                if (string.IsNullOrEmpty(GoogleAccountName) || string.IsNullOrWhiteSpace(GoogleAccountName)) {
                    issues.Add("No Google account has been configured");
                }
            }

            return issues;
        }

        private ushort EmailSystem { get; set; }
        private string GoogleAccountName { get; set; }
        private string SmtpHostName { get; set; }
        private ushort SmtpHostPort { get; set; }
        private string SmtpUsername { get; set; }
        private string SmtpPassword { get; set; }
        private string SmtpFromAddress { get; set; }

        private DateTime LastEmailSentTimestamp { get; set; } = DateTime.MinValue;

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "EmailSystem":
                    EmailSystem = Properties.Settings.Default.EmailSystem;
                    break;

                case "GoogleAccountName":
                    GoogleAccountName = Properties.Settings.Default.GoogleAccountName;
                    break;

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

                case "SmtpFromAddress":
                    SmtpFromAddress = Security.Decrypt(Properties.Settings.Default.SmtpFromAddress);
                    break;
            }
        }
    }
}