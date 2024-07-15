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

namespace DaleGhent.NINA.GroundStation.Slack {

    [ExportMetadata("Name", "Send to Slack")]
    [ExportMetadata("Description", "Sends a freeform message to Slack")]
    [ExportMetadata("Icon", "Slack_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SendToSlack : SequenceItem, IValidatable {
        private Channel channel = null;
        private string message = string.Empty;

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

        private IWindowService windowService;
        private readonly IMetadata metadata;

        [ImportingConstructor]
        public SendToSlack(ICameraMediator cameraMediator,
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
        }

        public SendToSlack(SendToSlack copyMe) : this(cameraMediator: copyMe.cameraMediator,
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
        public Channel Channel {
            get {
                channel ??= Channels.FirstOrDefault();
                return channel;
            }
            set {
                channel = value;
                RaisePropertyChanged();
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

        public string MessagePreview {
            get {
                string text = string.Empty;
                byte mesgPreviewLen = 50;

                if (!string.IsNullOrEmpty(message)) {
                    var count = message.Length > mesgPreviewLen ? mesgPreviewLen : message.Length;
                    text = message[..count];

                    if (message.Length > mesgPreviewLen) {
                        text += "...";
                    }
                }

                return text;
            }
        }

        public static ObservableCollection<Channel> Channels => GroundStation.GroundStationConfig.SlackChannels;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var message = Utilities.Utilities.ResolveTokens(Message, this, metadata);

            var slack = new SlackClient();
            await slack.PostMessage(channel, message);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = SlackClient.CommonValidations();

            if (string.IsNullOrEmpty(message)) {
                i.Add("Slack message is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToSlack(this) {
                Channel = Channel,
                Message = Message,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Channel: {Channel}";
        }

        public IWindowService WindowService {
            get {
                windowService ??= new WindowService();
                return windowService;
            }

            set => windowService = value;
        }

        [RelayCommand]
        private async Task OpenConfigurationWindow(object o) {
            var conf = new SendToSlackSetup() {
                Channels = Channels,
                Channel = Channel,
                Message = Message,
            };

            await WindowService.ShowDialog(conf, "Send to Slack", System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            Channel = conf.Channel;
            Message = conf.Message;
        }
    }

    public partial class SendToSlackSetup : BaseINPC {
        [ObservableProperty]
        private Channel channel;

        [ObservableProperty]
        private ObservableCollection<Channel> channels;

        [ObservableProperty]
        private string message;
    }

}