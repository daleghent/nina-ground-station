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
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.NtfySh {

    [ExportMetadata("Name", "Send to ntfy")]
    [ExportMetadata("Description", "Posts a message to a ntfy topic")]
    [ExportMetadata("Icon", "ntfy_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SendToNtfySh : SequenceItem, IValidatable {
        private string message = string.Empty;
        private string title = string.Empty;
        private string tags = string.Empty;
        private NtfyShPriorityLevels priority = NtfyShPriorityLevels.Default;

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
        public SendToNtfySh(ICameraMediator cameraMediator,
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

        public SendToNtfySh(SendToNtfySh copyMe) : this(
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
        public string Tags {
            get => tags;
            set {
                tags = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public NtfyShPriorityLevels Priority {
            get => priority;
            set {
                priority = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var resolvedTitle = Utilities.Utilities.ResolveTokens(title, this, metadata);
            var resolvedMessage = Utilities.Utilities.ResolveTokens(message, this, metadata);
            var resolvedTags = Utilities.Utilities.ResolveTokens(tags, this, metadata);

            try {
                var ntfySh = new NtfySh {
                    NtfyShTitle = resolvedTitle,
                    NtfyShMessage = resolvedMessage,
                    NtfyShTags = resolvedTags,
                    NtfyShPrioirty = NtfySh.GsNtfyPrio2PriorityLevel(priority),
                };

                await ntfySh.SendNftyShMessage();

            } catch (Exception ex) {
                throw new SequenceEntityFailedException($"Failed to send ntfy message: {ex.Message}");
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(title)) {
                i.Add("There is no message title");
            }

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
            return new SendToNtfySh(this) {
                Message = Message,
                Title = Title,
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
            var conf = new NtfyMessage() {
                Title = title,
                Message = message,
                Tags = tags,
                Priority = priority,
            };

            await WindowService.ShowDialog(conf, "Send to ntfy", System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            Message = conf.Message;
            Title = conf.Title;
            Tags = conf.Tags;
            Priority = conf.Priority;
        }
    }

    public partial class NtfyMessage : BaseINPC {
        [ObservableProperty]
        public string message;

        [ObservableProperty]
        public string title;

        [ObservableProperty]
        public string tags;

        [ObservableProperty]
        public NtfyShPriorityLevels priority;
    }
}