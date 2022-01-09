#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Email;
using DaleGhent.NINA.GroundStation.Utilities;
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
        private EmailCommon email;
        private ISequenceItem previousItem;
        private string recipient;

        [ImportingConstructor]
        public FailuresToEmailTrigger() {
            email = new EmailCommon();

            SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
            SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
            Recipient = SmtpDefaultRecipients;

            EmailFailureSubjectText = Properties.Settings.Default.EmailFailureSubjectText;
            EmailFailureBodyText = Properties.Settings.Default.EmailFailureBodyText;

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
            foreach (var failedItem in FailedItems) {
                var subject = Utilities.Utilities.ResolveTokens(EmailFailureSubjectText, previousItem);
                var body = Utilities.Utilities.ResolveTokens(EmailFailureBodyText, previousItem);

                subject = Utilities.Utilities.ResolveFailureTokens(subject, failedItem);
                body = Utilities.Utilities.ResolveFailureTokens(body, failedItem);

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(SmtpFromAddress));
                message.To.AddRange(InternetAddressList.Parse(Recipient));
                message.Subject = subject;
                message.Body = new TextPart("plain") { Text = body };

                await email.SendEmail(message, ct);
            }

            FailedItems.Clear();
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (previousItem == null) {
                Logger.Debug("Previous item is null. Asserting false");
                return false;
            }

            this.previousItem = previousItem;

            if (this.previousItem.Name.Contains("Email") && this.previousItem.Category.Equals(Category)) {
                Logger.Debug("Previous item is related. Asserting false");
                return false;
            }

            FailedItems.Clear();
            FailedItems = Utilities.Utilities.GetFailedItems(this.previousItem);

            return FailedItems.Count > 0;
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

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToEmailTrigger(this) {
                Recipient = Recipient,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToEmailTrigger)}";
        }

        private string SmtpFromAddress { get; set; }
        private string SmtpDefaultRecipients { get; set; }
        private string EmailFailureSubjectText { get; set; }
        private string EmailFailureBodyText { get; set; }

        private List<FailedItem> FailedItems { get; set; } = new List<FailedItem>();

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "SmtpFromAddress":
                    SmtpFromAddress = Properties.Settings.Default.SmtpFromAddress;
                    break;

                case "SmtpDefaultRecipients":
                    SmtpDefaultRecipients = Properties.Settings.Default.SmtpDefaultRecipients;
                    break;

                case "EmailFailureSubjectText":
                    EmailFailureSubjectText = Properties.Settings.Default.EmailFailureSubjectText;
                    break;

                case "EmailFailureBodyText":
                    EmailFailureBodyText = Properties.Settings.Default.EmailFailureBodyText;
                    break;
            }
        }
    }
}