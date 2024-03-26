#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using Discord;
using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
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
    public class SendToDiscordWebhook : SequenceItem, IValidatable {
        private string message = string.Empty;
        private string title = string.Empty;

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
        }

        public SendToDiscordWebhook() {
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

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var edgeColor = GroundStation.GroundStationConfig.DiscordMessageEdgeColor;

            var embed = new EmbedBuilder {
                Title = Utilities.Utilities.ResolveTokens(title, this, metadata),
                Color = new Color(edgeColor.R, edgeColor.G, edgeColor.B),
                Author = new EmbedAuthorBuilder {
                    Name = GroundStation.GroundStationConfig.DiscordImagePostTitle,
                },
                Timestamp = DateTimeOffset.UtcNow,
            };

            embed.AddField(Loc.Instance["LblMessage"], Utilities.Utilities.ResolveTokens(message, this, metadata));

            var discordWebhookCommon = new DiscordWebhookCommon();
            await discordWebhookCommon.SendDiscordWebook(embed);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.DiscordWebhookDefaultUrl)) {
                i.Add("Webhook URL is missing");
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
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}