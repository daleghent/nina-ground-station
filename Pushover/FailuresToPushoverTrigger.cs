#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Pushover;
using DaleGhent.NINA.GroundStation.Utilities;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using PushoverClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.FailuresToPushoverTrigger {

    [ExportMetadata("Name", "Failures to Pushover")]
    [ExportMetadata("Description", "Sends an event to Pushover when a sequence instruction fails")]
    [ExportMetadata("Icon", "Pushover_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToPushoverTrigger : SequenceTrigger, IValidatable {
        private PushoverCommon pushover;
        private ISequenceItem previousItem;
        private Priority priority;
        private NotificationSound notificationSound;

        [ImportingConstructor]
        public FailuresToPushoverTrigger() {
            pushover = new PushoverCommon();

            PushoverFailureTitleText = Properties.Settings.Default.PushoverFailureTitleText;
            PushoverFailureBodyText = Properties.Settings.Default.PushoverFailureBodyText;
            NotificationSound = Properties.Settings.Default.PushoverDefaultFailureSound;
            Priority = Properties.Settings.Default.PushoverDefaultFailurePriority;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToPushoverTrigger(FailuresToPushoverTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public Priority Priority {
            get => priority;
            set {
                priority = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public NotificationSound NotificationSound {
            get => notificationSound;
            set {
                notificationSound = value;
                RaisePropertyChanged();
            }
        }

        public Priority[] Priorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().Where(p => p != Priority.Emergency).ToArray();
        public NotificationSound[] NotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            foreach (var failedItem in FailedItems) {
                var title = Utilities.Utilities.ResolveTokens(PushoverFailureTitleText, previousItem);
                var message = Utilities.Utilities.ResolveTokens(PushoverFailureBodyText, previousItem);

                title = Utilities.Utilities.ResolveFailureTokens(title, failedItem);
                message = Utilities.Utilities.ResolveFailureTokens(message, failedItem);

                var newCts = new CancellationTokenSource();
                using (ct.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                    await pushover.PushMessage(title, message, Priority, NotificationSound, newCts.Token);
                }
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

            if (this.previousItem.Name.Contains("Pushover") && this.previousItem.Category.Equals(Category)) {
                Logger.Debug("Previous item is related. Asserting false");
                return false;
            }

            FailedItems.Clear();
            FailedItems = Utilities.Utilities.GetFailedItems(this.previousItem);

            return FailedItems.Count > 0;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(pushover.ValidateSettings());

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToPushoverTrigger(this) {
                Priority = Priority,
                NotificationSound = NotificationSound,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToPushoverTrigger)}";
        }

        private List<FailedItem> FailedItems { get; set; } = new List<FailedItem>();

        private string PushoverFailureTitleText { get; set; }
        private string PushoverFailureBodyText { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverFailureTitleText":
                    PushoverFailureTitleText = Properties.Settings.Default.PushoverFailureTitleText;
                    break;

                case "PushoverFailureBodyText":
                    PushoverFailureBodyText = Properties.Settings.Default.PushoverFailureBodyText;
                    break;
            }
        }
    }
}