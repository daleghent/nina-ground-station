#region "copyright"

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
        private ISequenceItem previousItem;
        private Priority priority = Priority.High;
        private NotificationSound notificationSound = NotificationSound.Pushover;

        [ImportingConstructor]
        public FailuresToPushoverTrigger() {
            PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);

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
            string title = $"Failure running {previousItem.Name}!";
            string message = $"{previousItem.Name} failed to run after {previousItem.Attempts} attempts!";

            var pclient = new Pushover(PushoverAppKey, PushoverUserKey);
             
            Logger.Debug("PushoverTrigger: Pushing message");
            var response = await pclient.PushAsync(title, message, priority: Priority, notificationSound: NotificationSound);
            Logger.Debug("foo2");

            if (response.Status != 1 || response.Errors?.Count > 0) {
                Logger.Error($"PushoverTrigger: Push failed. Status={response.Status}, Errors={response.Errors.Select(array => string.Join(", ", array))}");
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (previousItem == null) {
                Logger.Debug("PushoverTrigger: Previous item is null. Asserting false"); 
                return false;
            }

            this.previousItem = previousItem;

            if (this.previousItem.Status == SequenceEntityStatus.FAILED && !this.previousItem.Name.Contains("Pushover")) {
                Logger.Debug($"PushoverTrigger: Previous item \"{this.previousItem.Name}\" failed. Asserting true");
                return true;
            }

            Logger.Debug($"PushoverTrigger: Previous item \"{this.previousItem.Name}\" did not fail. Asserting false");
            return false;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(PushoverAppKey)) {
                i.Add("Pushover app key is missing");
            }

            if (string.IsNullOrEmpty(PushoverUserKey)) {
                i.Add("Pushover user key is missing");
            }

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

        private string PushoverAppKey { get; set; }
        private string PushoverUserKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverAppKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
                    break;
                case "PushoverUserKey":
                    PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
                    break;
            }
        }
    }
}