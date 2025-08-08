#region "copyright"

/*
    Copyright (c) 2024-2025 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaleGhent.NINA.GroundStation.MetadataClient;
using DaleGhent.NINA.GroundStation.Telegram;
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
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.SendToTelegram {

    [ExportMetadata("Name", "Send to Telegram")]
    [ExportMetadata("Description", "Sends a free form message to Telegram")]
    [ExportMetadata("Icon", "Telegram_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SendToTelegram : SequenceItem, IValidatable {
        private readonly TelegramCommon telegram;
        private string message = string.Empty;
        private bool doNotNotify = false;

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
        public SendToTelegram(ICameraMediator cameraMediator,
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

            telegram = new TelegramCommon();
        }

        public SendToTelegram() {
            telegram = new TelegramCommon();
        }

        public SendToTelegram(SendToTelegram copyMe) : this(cameraMediator: copyMe.cameraMediator,
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
        public bool DoNotNotify {
            get => doNotNotify;
            set {
                doNotNotify = value;

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

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var message = Utilities.Utilities.ResolveTokens(Message, this, metadata);

            await TelegramCommon.SendTelegram(message, DoNotNotify, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(TelegramCommon.ValidateSettings());

            if (string.IsNullOrEmpty(Message)) {
                i.Add("Telegram message is empty!");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToTelegram(this) {
                Message = Message,
                DoNotNotify = DoNotNotify,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
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
            var conf = new SendToTelegramSetup() {
                Message = message,
                DoNotNotify = doNotNotify,
            };

            await WindowService.ShowDialog(conf, Name, System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            Message = conf.Message;
            DoNotNotify = conf.DoNotNotify;
        }
    }

    public partial class SendToTelegramSetup : BaseINPC {
        [ObservableProperty]
        private string message;

        [ObservableProperty]
        private bool doNotNotify;

    }

}