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
            foreach (var failedItem in FailedItems) {
                var message = Utilities.Utilities.ResolveTokens(TelegramFailureBodyText, previousItem);
                message = Utilities.Utilities.ResolveFailureTokens(message, failedItem);

                await telegram.SendTelegram(message, true, ct);
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

            if (this.previousItem.Name.Contains("Telegram") && this.previousItem.Category.Equals(Category)) {
                Logger.Debug("Previous item is related. Asserting false");
                return false;
            }

            FailedItems.Clear();
            FailedItems = Utilities.Utilities.GetFailedItems(this.previousItem);

            return FailedItems.Count > 0;
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
            return new FailuresToTelegramTrigger(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToTelegramTrigger)}";
        }

        private List<Utilities.FailedItem> FailedItems { get; set; } = new List<Utilities.FailedItem>();

        private string TelegramFailureBodyText { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TelegramFailureBodyText":
                    TelegramFailureBodyText = Properties.Settings.Default.TelegramFailureBodyText;
                    break;
            }
        }
    }
}