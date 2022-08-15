﻿#region "copyright"

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
            message.To.AddRange(InternetAddressList.Parse(Recipient));
            message.Subject = Utilities.Utilities.ResolveTokens(Subject, this);
            message.Body = new TextPart("plain") { Text = Utilities.Utilities.ResolveTokens(Body, this) };

            try {
                await email.SendMessage(message, ct);
            } catch (Exception ex) {
                throw new SequenceEntityFailedException(ex.Message);
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(email.ValidateSettings());

            if (string.IsNullOrEmpty(Recipient) || string.IsNullOrWhiteSpace(Recipient)) {
                i.Add("Email recipient is missing");
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
            return new SendToEmail(this) {
                Recipient = Recipient,
                Subject = Subject,
                Body = Body,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToEmail)}";
        }

        private string SmtpDefaultRecipients { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "SmtpDefaultRecipients":
                    SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
                    Recipient = SmtpDefaultRecipients;
                    break;
            }
        }
    }
}