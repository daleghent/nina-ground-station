#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DaleGhent.NINA.GroundStation.HTTP {

    [ExportMetadata("Name", "Send HTTP Request")]
    [ExportMetadata("Description", "Send a HTTP GET or POST request to a URL")]
    [ExportMetadata("Icon", "HTTP_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class HttpClient : SequenceItem, IValidatable {
        private HttpMethodEnum httpMethod = HttpMethodEnum.GET;
        private string httpUri = string.Empty;
        private string httpPostBody = string.Empty;

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
        public HttpClient(ICameraMediator cameraMediator,
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

        public HttpClient(HttpClient copyMe) : this(
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
        public HttpMethodEnum HttpMethod {
            get => httpMethod;
            set {
                httpMethod = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string HttpUri {
            get => httpUri;
            set {
                httpUri = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string HttpPostBody {
            get => httpPostBody;
            set {
                httpPostBody = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var client = new System.Net.Http.HttpClient();
            var response = new HttpResponseMessage();
            var resolvedUri = Utilities.Utilities.ResolveTokens(httpUri, this, metadata, true);

            try {
                if (HttpMethod == HttpMethodEnum.GET) {
                    response = await client.GetAsync(resolvedUri, ct);
                } else if (HttpMethod == HttpMethodEnum.POST) {
                    var body = HttpUtility.UrlEncode(Utilities.Utilities.ResolveTokens(httpPostBody, this));
                    HttpContent httpContent = new StringContent(body);

                    response = await client.PostAsync(resolvedUri, httpContent, ct);
                } else {
                    throw new SequenceEntityFailedException($"Unsupported HTTP method {HttpMethod}");
                }

                var error = $"HTTP {HttpMethod} {HttpUri} returned status code {response.StatusCode}";

                if (((int)response.StatusCode) >= 400) {
                    throw new SequenceEntityFailedException(error);
                } else {
                    Logger.Info(error);
                }
            } catch (Exception ex) {
                if (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException) {
                    throw new SequenceEntityFailedException($"HTTP {HttpMethod} {HttpUri} failed: {ex.Message}");
                }

                throw;
            } finally {
                response.Dispose();
            }
        }

        public HttpMethodEnum[] HttpMethods => Enum.GetValues(typeof(HttpMethodEnum)).Cast<HttpMethodEnum>().ToArray();

        public enum HttpMethodEnum {
            GET,
            POST,
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(HttpUri) || string.IsNullOrWhiteSpace(HttpUri)) {
                i.Add("URL is missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new HttpClient(this) {
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
                HttpPostBody = HttpPostBody,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(HttpClient)}";
        }
    }
}