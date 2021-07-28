#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
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

namespace DaleGhent.NINA.GroundStation.SendToPushover {

    [ExportMetadata("Name", "Send to Pushover")]
    [ExportMetadata("Description", "Sends a free form message to Pushover")]
    [ExportMetadata("Icon", "Pushover_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToPushover : SequenceItem, IValidatable {
        private string title = string.Empty;
        private string message = string.Empty;
        private Priority priority;
        private NotificationSound notificationSound;

        [ImportingConstructor]
        public SendToPushover() {
            PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
            NotificationSound = Properties.Settings.Default.PushoverDefaultNotificationSound;
            Priority = Properties.Settings.Default.PushoverDefaultNotificationPriority;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public SendToPushover(SendToPushover copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Title {
            get => title;
            set {
                title = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Message {
            get => message;
            set {
                message = value;
                RaisePropertyChanged();
            }
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

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var pclient = new Pushover(PushoverAppKey, PushoverUserKey);

            Logger.Debug("PushoverTrigger: Pushing message");
            var response = await pclient.PushAsync(Title, Message, priority: Priority, notificationSound: NotificationSound);

            if (response.Status != 1 || response.Errors?.Count > 0) {
                Logger.Error($"PushoverTrigger: Push failed. Status={response.Status}, Errors={response.Errors.Select(array => string.Join(", ", array))}");
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(PushoverAppKey) || string.IsNullOrWhiteSpace(PushoverAppKey)) {
                i.Add("Pushover app key is missing");
            }

            if (string.IsNullOrEmpty(PushoverUserKey) || string.IsNullOrWhiteSpace(PushoverUserKey)) {
                i.Add("Pushover user key is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToPushover() {
                Icon = Icon,
                Name = Name,
                Title = Title,
                Message = Message,
                Priority = Priority,
                NotificationSound = NotificationSound,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToPushover)}";
        }

        private string PushoverAppKey { get; set; }
        private string PushoverUserKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverAppKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
                    break;
                case "PushoverUserKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
                    break;
            }
        }
    }
}