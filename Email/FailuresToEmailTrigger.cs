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
using NINA.Sequencer.Utility;
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
        private string recipient;

        private ISequenceRootContainer failureHook;
        private BackgroundQueueWorker<SequenceEntityFailureEventArgs> queueWorker;

        [ImportingConstructor]
        public FailuresToEmailTrigger() {
            queueWorker = new BackgroundQueueWorker<SequenceEntityFailureEventArgs>(1000, WorkerFn);
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

        public override void Initialize() {
            queueWorker.Stop();
            _ = queueWorker.Start();
        }

        public override void Teardown() {
            queueWorker.Stop();
        }

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null && failureHook != null) {
                // When trigger is removed from sequence, unregister event handler
                // This could potentially be skipped by just using weak events instead
                failureHook.FailureEvent -= Root_FailureEvent;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                queueWorker.Stop();
                // When dragging the item into the sequence while the sequence is already running
                // Make sure to register the event handler as "SequenceBlockInitialized" is already done
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
                _ = queueWorker.Start();
            }
            base.AfterParentChanged();
        }

        public override void SequenceBlockInitialize() {
            // Register failure event when the parent context starts
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent += Root_FailureEvent;
            }
            base.SequenceBlockInitialize();
        }

        public override void SequenceBlockTeardown() {
            // Unregister failure event when the parent context ends
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent -= Root_FailureEvent;
            }
        }

        private async Task Root_FailureEvent(object arg1, SequenceEntityFailureEventArgs arg2) {
            if (arg2.Entity == null) {
                // An exception without context has occurred. Not sure when this can happen
                // Todo: Might be worthwile to send in a different style
                return;
            }

            if (arg2.Entity is FailuresToEmailTrigger || arg2.Entity is SendToEmail.SendToEmail) {
                // Prevent email items to send email failures
                return;
            }

            await queueWorker.Enqueue(arg2);
        }

        private async Task WorkerFn(SequenceEntityFailureEventArgs item, CancellationToken token) {
            var failedItem = FailedItem.FromEntity(item.Entity, item.Exception);

            var subject = Utilities.Utilities.ResolveTokens(EmailFailureSubjectText, item.Entity);
            var body = Utilities.Utilities.ResolveTokens(EmailFailureBodyText, item.Entity);

            subject = Utilities.Utilities.ResolveFailureTokens(subject, failedItem);
            body = Utilities.Utilities.ResolveFailureTokens(body, failedItem);

            var attempts = 3; // Todo: Make it configurable?
            for (int i = 0; i < attempts; i++) {
                try {
                    var message = new MimeMessage();
                    message.From.Add(MailboxAddress.Parse(SmtpFromAddress));
                    message.To.AddRange(InternetAddressList.Parse(Recipient));
                    message.Subject = subject;
                    message.Body = new TextPart("plain") { Text = body };

                    var newCts = new CancellationTokenSource();
                    using (token.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                        await email.SendEmail(message, newCts.Token);
                    }
                } catch (Exception ex) {
                    Logger.Error($"Failed to send message. Attempt {i + 1}/{attempts}", ex);
                }
            }
        }

        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            return Task.CompletedTask;
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
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