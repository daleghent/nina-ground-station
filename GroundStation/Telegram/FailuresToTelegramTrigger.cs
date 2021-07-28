﻿#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace DaleGhent.NINA.GroundStation.FailuresToTelegramTrigger {

    [ExportMetadata("Name", "Failures to Telegram")]
    [ExportMetadata("Description", "Sends an event to Telegram when a sequence instruction fails")]
    [ExportMetadata("Icon", "Telegram_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToTelegramTrigger : SequenceTrigger, IValidatable {
        private ISequenceItem previousItem;

        [ImportingConstructor]
        public FailuresToTelegramTrigger() {
            TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
            TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToTelegramTrigger(FailuresToTelegramTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            string message = $"{previousItem.Name} failed to run after {previousItem.Attempts} attempt{((previousItem.Attempts > 1) ? string.Format("s") : string.Format(""))}!";

            if (PreviousItemIssues.Count > 0) {
                message += $"\nReason{((PreviousItemIssues.Count > 1) ? string.Format("s") : string.Format(""))}: {string.Join(", ", PreviousItemIssues)}";
            }

            var bclient = new TelegramBotClient(TelegramAccessToken);

            Logger.Debug("TelegramTrigger: Pushing message");

            try {
                await bclient.SendTextMessageAsync(TelegramChatId, message, cancellationToken: ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to Telegram: {ex.Message}");
                throw ex;
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            bool shouldTrigger = false;

            if (previousItem == null) {
                Logger.Debug("TelegramTrigger: Previous item is null. Asserting false");
                return shouldTrigger; ;
            }

            this.previousItem = previousItem;

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("Telegram")) {
                Logger.Debug($"TelegramTrigger: Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                shouldTrigger = true;

                if (this.previousItem is IValidatable validatableItem && validatableItem.Issues.Count > 0) {
                    PreviousItemIssues = validatableItem.Issues;
                    Logger.Debug($"TelegramTrigger: Previous item \"{this.previousItem.Name}\" had {PreviousItemIssues.Count} issues: {string.Join(", ", PreviousItemIssues)}");
                }
            } else {
                Logger.Debug($"TelegramTrigger: Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            }

            return shouldTrigger;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(TelegramAccessToken) || string.IsNullOrWhiteSpace(TelegramAccessToken)) {
                i.Add("Telegram bot access token is missing");
            }

            if (string.IsNullOrEmpty(TelegramChatId) || string.IsNullOrWhiteSpace(TelegramChatId)) {
                i.Add("Telegram chat ID missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToTelegramTrigger() {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToTelegramTrigger)}";
        }

        private string TelegramAccessToken { get; set; }
        private string TelegramChatId { get; set; }
        private IList<string> PreviousItemIssues { get; set; } = new List<string>();

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TelegramAccessToken":
                    TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
                    break;
                case "TelegramChatId":
                    TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);
                    break;
            }
        }
    }
}