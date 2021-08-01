#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Email;
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
        private EmailCommon email;
        private string recipient;
        private string subject = string.Empty;
        private string body = string.Empty;

        [ImportingConstructor]
        public SendToEmail() {
            email = new EmailCommon();

            SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
            SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
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

            await email.SendEmail(message, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(email.ValidateSettings());

            if (string.IsNullOrEmpty(Recipient) || string.IsNullOrWhiteSpace(Recipient)) {
                i.Add("Email recipient is missing");
            }

            if (string.IsNullOrEmpty(SmtpFromAddress) || string.IsNullOrWhiteSpace(SmtpFromAddress)) {
                i.Add("Email from address is missing");
            }

            if (string.IsNullOrEmpty(Subject) || string.IsNullOrWhiteSpace(Subject)) {
                i.Add("Email subject is missing");
            }

            if (string.IsNullOrEmpty(Body) || string.IsNullOrWhiteSpace(Body)) {
                i.Add("Email body is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

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

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
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