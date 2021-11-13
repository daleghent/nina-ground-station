#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Pushover;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using PushoverClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private PushoverCommon pushover;
        private string title = string.Empty;
        private string message = string.Empty;
        private Priority priority;
        private NotificationSound notificationSound;

        [ImportingConstructor]
        public SendToPushover() {
            pushover = new PushoverCommon();

            NotificationSound = Properties.Settings.Default.PushoverDefaultNotificationSound;
            Priority = Properties.Settings.Default.PushoverDefaultNotificationPriority;
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
            var title = Utilities.ResolveTokens(Title, this);
            var message = Utilities.ResolveTokens(Message, this);

            await pushover.PushMessage(title, message, Priority, NotificationSound, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(pushover.ValidateSettings());

            if (string.IsNullOrEmpty(Title) || string.IsNullOrWhiteSpace(Title)) {
                i.Add("Pushover message title is missing");
            }

            if (string.IsNullOrEmpty(Message) || string.IsNullOrWhiteSpace(Message)) {
                i.Add("Pushover message body is missing");
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
    }
}