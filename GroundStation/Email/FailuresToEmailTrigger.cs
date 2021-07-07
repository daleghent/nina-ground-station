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
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.FailuresToEmailTrigger {

    [ExportMetadata("Name", "Failures to Email")]
    [ExportMetadata("Description", "Sends an event via email when a sequence instruction fails")]
    [ExportMetadata("Icon", "Email_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToEmailTrigger : SequenceTrigger, IValidatable {
        private ISequenceItem previousItem;
        private ISequenceItem nextItem;
        private string recipient;

        [ImportingConstructor]
        public FailuresToEmailTrigger() {
            SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
            SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
            SmtpHostName = Properties.Settings.Default.SmtpHostName;
            SmtpHostPort = Properties.Settings.Default.SmtpHostPort;
            SmtpUsername = Security.Decrypt(Properties.Settings.Default.SmtpUsername);
            SmtpPassword = Security.Decrypt(Properties.Settings.Default.SmtpPassword);
            Recipient = SmtpDefaultRecipients;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToEmailTrigger(FailuresToEmailTrigger copyMe) : this() {
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

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            string subject = $"Failure running {previousItem.Name}!";
            string body = $"Time: {DateTime.Now}\nStatus: \"{previousItem.Name}\" did not complete successfully after {previousItem.Attempts} attempts!";

            if (this.nextItem != null) {
                body += $"\nFollowing instruction: \"{nextItem.Name}\"";
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(SmtpFromAddress));
            message.To.AddRange(InternetAddressList.Parse(Recipient));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

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
                Logger.Error($"FailuresToEmail: Connection to {SmtpHostPort}:{SmtpHostPort} failed: {ex.SocketErrorCode}: {ex.Message}");
                throw ex;
            } catch (AuthenticationException ex) {
                Logger.Error($"FailuresToEmail: User {SmtpUsername} failed to authenticate with {SmtpHostName}:{SmtpHostPort}");
                throw ex;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (previousItem == null) {
                Logger.Debug("FailuresToEmail: Previous item is null. Asserting false");
                return false;
            }

            this.previousItem = previousItem;

            if (nextItem != null) {
                this.nextItem = nextItem;
            }

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("Pushover")) {
                Logger.Debug($"FailuresToEmail: Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                return true;
            }

            Logger.Debug($"FailuresToEmail: Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            return false;
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

            if (string.IsNullOrEmpty(SmtpHostName)) {
                i.Add("SMTP server is not configured");
            }

            if (SmtpHostPort < 1) {
                i.Add("SMTP port is invalid");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToEmailTrigger() {
                Icon = Icon,
                Name = Name,
                Recipient = Recipient,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToEmailTrigger)}";
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
                    SmtpUsername = Security.Decrypt(Properties.Settings.Default.SmtpUsername);
                    break;
                case "SmtpPassword":
                    SmtpPassword = Security.Decrypt(Properties.Settings.Default.SmtpPassword);
                    break;
                case "SmtpFromAddress":
                    SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
                    break;
                case "SmtpDefaultRecipients":
                    SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
                    break;
            }
        }
    }
}