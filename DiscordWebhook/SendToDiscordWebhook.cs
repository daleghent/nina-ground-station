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
using Discord;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility.WindowService;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    [ExportMetadata("Name", "Send to Discord")]
    [ExportMetadata("Description", "Posts a message to a Discord channel")]
    [ExportMetadata("Icon", "Discord_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SendToDiscordWebhook : SequenceItem, IValidatable {
        private string message = string.Empty;
        private string title = string.Empty;
        private System.Windows.Media.Color discordMessageEdgeColor;

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
        private readonly DiscordWebhookCommon discordWebhookCommon;

        [ImportingConstructor]
        public SendToDiscordWebhook(ICameraMediator cameraMediator,
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

            metadata = new Metadata(cameraMediator, domeMediator, filterWheelMediator,
                flatDeviceMediator, focuserMediator, guiderMediator, rotatorMediator,
                safetyMonitorMediator, switchMediator, telescopeMediator, weatherDataMediator);

            discordMessageEdgeColor = GroundStation.GroundStationConfig.DiscordMessageEdgeColor;
            discordWebhookCommon = new DiscordWebhookCommon();

            Validate();
        }

        public SendToDiscordWebhook(SendToDiscordWebhook copyMe) : this(
                                                    cameraMediator: copyMe.cameraMediator,
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
                Validate();
            }
        }

        [JsonProperty]
        public string Message {
            get => message;
            set {
                message = value;
                RaisePropertyChanged();
                Validate();
            }
        }

        [JsonProperty]
        public System.Windows.Media.Color DiscordMessageEdgeColor {
            get => discordMessageEdgeColor;
            set {
                discordMessageEdgeColor = value;
                RaisePropertyChanged();
            }
        }

        public string MessagePreviewToolTip {
            get {
                string text = "No message configured";

                if (!string.IsNullOrEmpty(title)) {
                    text = title;
                } else if (!string.IsNullOrEmpty(message)) {
                    text = message[..50] + "...";
                }

                return text;
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var edgeColor = discordMessageEdgeColor;

            var embed = new EmbedBuilder {
                Color = new Discord.Color(edgeColor.R, edgeColor.G, edgeColor.B),
                Timestamp = DateTimeOffset.UtcNow,
            };

            embed.AddField(Utilities.Utilities.ResolveTokens(title, this, metadata), Utilities.Utilities.ResolveTokens(message, this, metadata));
            await discordWebhookCommon.SendDiscordWebook(embed);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = discordWebhookCommon.CommonValidation();

            if (string.IsNullOrEmpty(message)) {
                i.Add("There is no message content");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToDiscordWebhook(this) {
                Title = Title,
                Message = Message,
                DiscordMessageEdgeColor = DiscordMessageEdgeColor,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, Title: {Title}";
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
            var conf = new SendToDiscordWebhookSetup() {
                Title = title,
                Message = message,
                DiscordMessageEdgeColor = discordMessageEdgeColor,
            };

            await WindowService.ShowDialog(conf, "Send to Discord", System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            Title = conf.Title;
            Message = conf.Message;
            DiscordMessageEdgeColor = conf.DiscordMessageEdgeColor;
        }
    }

    public partial class SendToDiscordWebhookSetup : BaseINPC {
        [ObservableProperty]
        private string title;

        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private System.Windows.Media.Color discordMessageEdgeColor;
    }
}