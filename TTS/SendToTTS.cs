#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org> and contributors

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.TTS {

    [ExportMetadata("Name", "Send to TTS")]
    [ExportMetadata("Description", "Plays a Text-To-Speech announcement from given message")]
    [ExportMetadata("Icon", "TTS_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToTTS : SequenceItem, IValidatable {
        private string message;

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
        public SendToTTS(ICameraMediator cameraMediator,
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

        public SendToTTS() {
        }

        public SendToTTS(SendToTTS copyMe) : this(cameraMediator: copyMe.cameraMediator,
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

        public override object Clone() {
            return new SendToTTS(this) {
                Message = Message,
            };
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
            var text = Utilities.Utilities.ResolveTokens(Message, this, metadata);

            await TTS.Speak(text, ct);
        }

        public IList<string> Issues { get; set; } = new List<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(Message) || string.IsNullOrWhiteSpace(Message)) {
                i.Add("TTS message is missing");
            }

            if (!TTS.HasVoice()) {
                i.Add("No Text-To-Speech voices found");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}