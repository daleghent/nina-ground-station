#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Ifttt;
using DaleGhent.NINA.GroundStation.MetadataClient;
using Newtonsoft.Json;
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

namespace DaleGhent.NINA.GroundStation.SendToIftttWebhook {

    [ExportMetadata("Name", "Send to IFTTT Webhooks")]
    [ExportMetadata("Description", "Sends a free form 3-value message to IFTTT Webhooks")]
    [ExportMetadata("Icon", "IFTTT_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToIftttWebhook : SequenceItem, IValidatable {
        private readonly IftttCommon ifttt;
        private string eventName = "nina";
        private string value1 = string.Empty;
        private string value2 = string.Empty;
        private string value3 = string.Empty;

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
        public SendToIftttWebhook(ICameraMediator cameraMediator,
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

            ifttt = new IftttCommon();
        }

        public SendToIftttWebhook() {
            ifttt = new IftttCommon();
        }

        public SendToIftttWebhook(SendToIftttWebhook copyMe) : this(cameraMediator: copyMe.cameraMediator,
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
        public string EventName {
            get => eventName;
            set {
                if (!value.Contains('/')) {
                    eventName = value;
                }

                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value1 {
            get => value1;
            set {
                value1 = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value2 {
            get => value2;
            set {
                value2 = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Value3 {
            get => value3;
            set {
                value3 = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var dict = new Dictionary<string, string> {
                { "value1", Utilities.Utilities.ResolveTokens(Value1, this, metadata) },
                { "value2", Utilities.Utilities.ResolveTokens(Value2, this, metadata) },
                { "value3", Utilities.Utilities.ResolveTokens(Value3, this, metadata) }
            };

            await IftttCommon.SendIftttWebhook(JsonConvert.SerializeObject(dict), EventName, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(IftttCommon.ValidateSettings());

            if (string.IsNullOrEmpty(EventName)) {
                i.Add("IFTTT Webhook event name is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToIftttWebhook(this) {
                EventName = EventName,
                Value1 = Value1,
                Value2 = Value2,
                Value3 = Value3,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}