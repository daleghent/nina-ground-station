﻿#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Pushover;
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

            NotificationSound = Properties.Settings.Default.PushoverDefaultFailureSound;
            Priority = Properties.Settings.Default.PushoverDefaultFailurePriority;
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
            string title = $"Failure running {previousItem.Name}!";
            string message = $"{previousItem.Name} failed to run after {previousItem.Attempts} attempt{((previousItem.Attempts > 1) ? string.Format("s") : string.Format(""))}!";

            if (PreviousItemIssues.Count > 0) {
                message += $"\nReason{((PreviousItemIssues.Count > 1) ? string.Format("s") : string.Format(""))}: {string.Join(", ", PreviousItemIssues)}";
            }

            await pushover.PushMessage(title, message, Priority, NotificationSound, ct);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            bool shouldTrigger = false;

            if (previousItem == null) {
                Logger.Debug("Previous item is null. Asserting false");
                return shouldTrigger;
            }

            if (previousItem == this.previousItem) {
                Logger.Debug("Previous item has already been processed. Asserting false");
                return shouldTrigger;
            }


            this.previousItem = previousItem;

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("Pushover")) {
                Logger.Debug($"Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                shouldTrigger = true;

                if (this.previousItem is IValidatable validatableItem && validatableItem.Issues.Count > 0) {
                    PreviousItemIssues = validatableItem.Issues;
                    Logger.Debug($"Previous item \"{this.previousItem.Name}\" had {PreviousItemIssues.Count} issues: {string.Join(", ", PreviousItemIssues)}");
                }
            } else {
                Logger.Debug($"Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            }

            return shouldTrigger;
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
            return new FailuresToPushoverTrigger() {
                Icon = Icon,
                Name = Name,
                Priority = Priority,
                NotificationSound = NotificationSound,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToPushoverTrigger)}";
        }

        private IList<string> PreviousItemIssues { get; set; } = new List<string>();
    }
}