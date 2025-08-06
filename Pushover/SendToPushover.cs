#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaleGhent.NINA.GroundStation.MetadataClient;
using DaleGhent.NINA.GroundStation.PushoverClient;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.WindowService;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
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
    public partial class SendToPushover : SequenceItem, IValidatable {
        private readonly PushoverClient.PushoverClient pushover;
        private string title = string.Empty;
        private string message = string.Empty;
        private Priority priority;
        private NotificationSound notificationSound;

        private readonly ICameraMediator cameraMediator;
        private readonly IDomeMediator domeMediator;
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IGuiderMediator guiderMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly ISwitchMediator switchMediator;
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IWeatherDataMediator weatherDataMediator;

        private readonly IMetadata metadata;
        private IWindowService windowService;

        [ImportingConstructor]
        public SendToPushover(ICameraMediator cameraMediator,
                             IDomeMediator domeMediator,
                             IFilterWheelMediator filterWheelMediator,
                             IFlatDeviceMediator flatDeviceMediator,
                             IFocuserMediator focuserMediator,
                             IGuiderMediator guiderMediator,
                             IRotatorMediator rotatorMediator,
                             ISafetyMonitorMediator safetyMonitorMediator,
                             ISwitchMediator switchMediator,
                             ITelescopeMediator telescopeMediator,
                             IWeatherDataMediator weatherDataMediator) {
            this.cameraMediator = cameraMediator;
            this.domeMediator = domeMediator;
            this.guiderMediator = guiderMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.focuserMediator = focuserMediator;
            this.guiderMediator = guiderMediator;
            this.rotatorMediator = rotatorMediator;
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.switchMediator = switchMediator;
            this.telescopeMediator = telescopeMediator;
            this.weatherDataMediator = weatherDataMediator;

            metadata = new Metadata(cameraMediator,
                domeMediator, filterWheelMediator, flatDeviceMediator, focuserMediator,
                guiderMediator, rotatorMediator, safetyMonitorMediator, switchMediator,
                telescopeMediator, weatherDataMediator);

            pushover = new PushoverClient.PushoverClient();

            NotificationSound = GroundStation.GroundStationConfig.PushoverDefaultNotificationSound;
            Priority = GroundStation.GroundStationConfig.PushoverDefaultNotificationPriority;
        }

        public SendToPushover() {
            pushover = new PushoverClient.PushoverClient();

            NotificationSound = GroundStation.GroundStationConfig.PushoverDefaultNotificationSound;
            Priority = GroundStation.GroundStationConfig.PushoverDefaultNotificationPriority;
        }

        public SendToPushover(SendToPushover copyMe) : this(cameraMediator: copyMe.cameraMediator,
                                                            domeMediator: copyMe.domeMediator,
                                                            filterWheelMediator: copyMe.filterWheelMediator,
                                                            flatDeviceMediator: copyMe.flatDeviceMediator,
                                                            focuserMediator: copyMe.focuserMediator,
                                                            guiderMediator: copyMe.guiderMediator,
                                                            rotatorMediator: copyMe.rotatorMediator,
                                                            safetyMonitorMediator: copyMe.safetyMonitorMediator,
                                                            switchMediator: copyMe.switchMediator,
                                                            telescopeMediator: copyMe.telescopeMediator,
                                                            weatherDataMediator: copyMe.weatherDataMediator) {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Title {
            get => title;
            set {
                title = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MessagePreview));
                Validate();
            }
        }

        [JsonProperty]
        public string Message {
            get => message;
            set {
                message = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MessagePreview));
                Validate();
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

        public static Priority[] Priorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().ToArray();
        public static NotificationSound[] NotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

        public string MessagePreview {
            get {
                string text = string.Empty;
                byte mesgPreviewLen = 50;

                if (!string.IsNullOrEmpty(title)) {
                    text = title;
                } else if (!string.IsNullOrEmpty(message)) {
                    var count = message.Length > mesgPreviewLen ? mesgPreviewLen : message.Length;
                    text = message[..count];

                    if (message.Length > mesgPreviewLen) {
                        text += "...";
                    }
                }

                return text;
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var title = Utilities.Utilities.ResolveTokens(Title, this, metadata);
            var message = Utilities.Utilities.ResolveTokens(Message, this, metadata);

            await PushoverClient.PushoverClient.PushMessage(title, message, Priority, NotificationSound, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(PushoverClient.PushoverClient.ValidateSettings());

            if (string.IsNullOrEmpty(Title)) {
                i.Add("Pushover message title is missing");
            }

            if (string.IsNullOrEmpty(Message)) {
                i.Add("Pushover message body is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToPushover(this) {
                Title = Title,
                Message = Message,
                Priority = Priority,
                NotificationSound = NotificationSound,

            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Title: {title}";
        }

        public IWindowService WindowService {
            get {
                windowService ??= new WindowService();
                return windowService;
            }

            set => windowService = value;
        }

        // This attribute will auto generate a RelayCommand for the method. It is called <methodname>Command -> OpenConfigurationWindowCommand. The class has to be marked as partial for it to work.
        [RelayCommand]
        private async Task OpenConfigurationWindow(object o) {
            var conf = new SendToPushoverSetup() {
                Title = title,
                Message = message,
                Priority = priority,
                NotificationSound = notificationSound
            };

            await WindowService.ShowDialog(conf, "Send to Pushover", System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            Title = conf.Title;
            Message = conf.Message;
            Priority = conf.Priority;
            NotificationSound = conf.NotificationSound;
        }
    }

    public partial class SendToPushoverSetup : BaseINPC {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private Priority priority;

        [ObservableProperty]
        private NotificationSound notificationSound;

        [ObservableProperty]
        private Priority[] priorities = SendToPushover.Priorities;

        [ObservableProperty]
        private NotificationSound[] notificationSounds = SendToPushover.NotificationSounds;
    }
}