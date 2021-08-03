#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Telegram;
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

namespace DaleGhent.NINA.GroundStation.FailuresToTelegramTrigger {

    [ExportMetadata("Name", "Failures to Telegram")]
    [ExportMetadata("Description", "Sends an event to Telegram when a sequence instruction fails")]
    [ExportMetadata("Icon", "Telegram_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToTelegramTrigger : SequenceTrigger, IValidatable {
        private TelegramCommon telegram;
        private ISequenceItem previousItem;

        [ImportingConstructor]
        public FailuresToTelegramTrigger() {
            telegram = new TelegramCommon();

            TelegramFailureBodyText = Properties.Settings.Default.TelegramFailureBodyText;
            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToTelegramTrigger(FailuresToTelegramTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var message = Utilities.ResolveTokens(TelegramFailureBodyText, previousItem);
            message = Utilities.ResolveFailureTokens(message, previousItem);

            await telegram.SendTelegram(message, true, ct);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
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
            var i = new List<string>(telegram.ValidateSettings());

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

        private IList<string> PreviousItemIssues { get; set; } = new List<string>();

        private string TelegramFailureBodyText { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TelegramFailureBodyText":
                    TelegramFailureBodyText = Properties.Settings.Default.TelegramFailureBodyText;
                    break;
            }
        }
    }
}