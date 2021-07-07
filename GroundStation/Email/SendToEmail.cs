﻿#region "copyright"

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
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.SendToEmail {

    [ExportMetadata("Name", "Send email")]
    [ExportMetadata("Description", "Sends an email")]
    [ExportMetadata("Icon", "Email_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToEmail : SequenceItem, IValidatable {
        private string recipient;
        private string subject = string.Empty;
        private string body = string.Empty;

        [ImportingConstructor]
        public SendToEmail() {
            SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
            SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
            SmtpHostName = Properties.Settings.Default.SmtpHostName;
            SmtpHostPort = Properties.Settings.Default.SmtpHostPort;
            SmtpUsername = Properties.Settings.Default.SmtpUsername;
            SmtpPassword = Properties.Settings.Default.SmtpPassword;
            Recipient = SmtpDefaultRecipients;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public SendToEmail(SendToEmail copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Recipient {
            get => recipient;
            set {
                recipient = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Subject {
            get => subject;
            set {
                subject = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Body {
            get => body;
            set {
                body = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(SmtpFromAddress));
            message.To.AddRange(InternetAddressList.Parse(Recipient));
            message.Subject = Subject;
            message.Body = new TextPart("plain") { Text = Body };

            var xMailerHeader = new Header("X-Mailer", "NINA");
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
                Logger.Error($"SmtpToEmail: Connection to {SmtpHostPort}:{SmtpHostPort} failed: {ex.SocketErrorCode}: {ex.Message}");
                throw ex;
            } catch (AuthenticationException ex) {
                Logger.Error($"SendToEmail: User {SmtpUsername} failed to authenticate with {SmtpHostName}:{SmtpHostPort}");
                throw ex;
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(Recipient)) {
                i.Add("Email recipient is missing");
            }

            if (string.IsNullOrEmpty(SmtpFromAddress)) {
                i.Add("Email from address is missing");
            }

            if (string.IsNullOrEmpty(Subject)) {
                i.Add("Email subject is missing");
            }

            if (string.IsNullOrEmpty(Body)) {
                i.Add("Email body is missing");
            }

            if (string.IsNullOrEmpty(SmtpHostName)) {
                i.Add("SMTP server is not configured");
            }

            if (SmtpHostPort < 1) {
                i.Add("SMTP port is invalid");
            }

            Issues = i;
            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToEmail() {
                Icon = Icon,
                Name = Name,
                Recipient = Recipient,
                Subject = Subject,
                Body = Body,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToEmail)}";
        }

        private string SmtpFromAddress { get; set; }
        private string SmtpDefaultRecipients { get; set; }
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
                    SmtpUsername = Properties.Settings.Default.SmtpUsername;
                    break;
                case "SmtpPassword":
                    SmtpPassword = Properties.Settings.Default.SmtpPassword;
                    break;
                case "SmtpFromAddress":
                    SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
                    break;
                case "SmtpDefaultRecipients":
                    SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
                    Recipient = SmtpDefaultRecipients;
                    break;
            }
        }
    }
}