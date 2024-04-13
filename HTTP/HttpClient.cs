#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static DaleGhent.NINA.GroundStation.HTTP.HttpClient;

namespace DaleGhent.NINA.GroundStation.HTTP {

    [ExportMetadata("Name", "Send HTTP Request")]
    [ExportMetadata("Description", "Send a HTTP GET or POST request to a URL")]
    [ExportMetadata("Icon", "HTTP_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class HttpClient : SequenceItem, IValidatable {
        private HttpMethodEnum httpMethod = HttpMethodEnum.GET;
        private string httpUri = string.Empty;
        private string httpPostBody = string.Empty;
        private string httpClientDescription = string.Empty;
        private string httpPostContentType = "text/plain";

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
                RaisePropertyChanged(nameof(HttpClientInstructionToolTip));
            }
        }

        [JsonProperty]
        public string HttpUri {
            get => httpUri;
            set {
                httpUri = value.Trim();
                Validate();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HttpClientInstructionToolTip));
            }
        }

        [JsonProperty]
        public string HttpPostContentType {
            get => httpPostContentType;
            set {
                httpPostContentType = string.IsNullOrEmpty(value) ? "text/plain" : value.Trim();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HttpClientInstructionToolTip));
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

        [JsonProperty]
        public string HttpClientDescription {
            get => httpClientDescription;
            set {
                httpClientDescription = value;
                RaisePropertyChanged();
            }
        }

        public string HttpClientInstructionToolTip {
            get {
                string text = "Not configured";

                if (!string.IsNullOrEmpty(HttpUri)) {
                    text = $"Method: {HttpMethod}{Environment.NewLine}URL: {HttpUri[..42]} ...";

                    if (HttpMethod == HttpMethodEnum.POST) {
                        text += $"{Environment.NewLine}Type: {HttpPostContentType}";
                    }
                }

                return text;
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            const string descriptionToken = @"$$DESCRIPTION$$";
            var client = new System.Net.Http.HttpClient();
            var response = new HttpResponseMessage();

            var resolvedUri = httpUri.Replace(descriptionToken, Utilities.Utilities.DoUrlEncode(true, httpClientDescription));
            resolvedUri = Utilities.Utilities.ResolveTokens(resolvedUri, this, metadata, true);

            client.DefaultRequestHeaders.ExpectContinue = false;

            try {
                if (HttpMethod == HttpMethodEnum.GET) {
                    response = await client.GetAsync(resolvedUri, ct);
                } else if (HttpMethod == HttpMethodEnum.POST) {
                    var body = httpPostBody.Replace(descriptionToken, httpClientDescription);
                    body = Utilities.Utilities.ResolveTokens(body, this);
                    HttpContent httpContent = new StringContent(body);

                    if (!string.IsNullOrEmpty(httpPostContentType)) {
                        httpContent.Headers.Remove("Content-Type");
                        httpContent.Headers.Add("Content-Type", httpPostContentType);
                    }

                    Logger.Debug($"Sending {HttpMethod} {HttpUri} ({HttpPostContentType}):{Environment.NewLine}Resolved URI: {resolvedUri}{Environment.NewLine}Reqest body:{Environment.NewLine}{body}");
                    response = await client.PostAsync(resolvedUri, httpContent, ct);
                } else {
                    throw new SequenceEntityFailedException($"Unsupported HTTP method {HttpMethod}");
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(ct);
                Logger.Info($"HTTP {HttpMethod} {HttpUri} ({HttpPostContentType}) returned status code {(int)response.StatusCode} ({response.StatusCode}). Response body:{Environment.NewLine}{content}");
            } catch (Exception ex) {
                if (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException) {
                    throw new SequenceEntityFailedException($"HTTP {HttpMethod} {HttpUri} ({HttpPostContentType}) failed: {ex.Message}");
                }

                throw;
            } finally {
                response.Dispose();
            }
        }

        public static HttpMethodEnum[] HttpMethods => Enum.GetValues(typeof(HttpMethodEnum)).Cast<HttpMethodEnum>().ToArray();

        public enum HttpMethodEnum {
            GET,
            POST,
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(HttpUri)) {
                i.Add("URL is missing");
                goto end;
            }

            if (!Uri.IsWellFormedUriString(HttpUri, UriKind.RelativeOrAbsolute)) {
                i.Add("URL format is invalid");
            }

        end:
            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new HttpClient(this) {
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
                HttpPostBody = HttpPostBody,
                HttpPostContentType = HttpPostContentType,
                HttpClientDescription = HttpClientDescription,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, {HttpClientDescription}. URL: {HttpMethod} {HttpUri}";
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
            var conf = new HttpClientSetup() {
                HttpMethod = httpMethod,
                HttpUri = httpUri,
                HttpPostContentType = httpPostContentType,
                HttpPostBody = httpPostBody,
                HttpClientDescription = httpClientDescription,
            };

            await WindowService.ShowDialog(conf, "HTTP Request Parameters", System.Windows.ResizeMode.CanResize, System.Windows.WindowStyle.ThreeDBorderWindow);

            HttpMethod = conf.HttpMethod;
            HttpUri = conf.HttpUri;
            HttpPostContentType = conf.HttpPostContentType;
            HttpPostBody = conf.HttpPostBody;
            HttpClientDescription = conf.HttpClientDescription;
        }
    }

    public partial class HttpClientSetup : BaseINPC {
        //This will create a public property for the class with the same name but a starting capital letter 
        //The generated property will have a getter and setter and the setter will automatically raise INotifyPropertyChanged so the UI can update automatically the value
        [ObservableProperty]
        private HttpMethodEnum httpMethod;

        [ObservableProperty]
        private string httpUri;

        [ObservableProperty]
        private string httpPostContentType;

        [ObservableProperty]
        private string httpPostBody;

        [ObservableProperty]
        private string httpClientDescription;
    }
}