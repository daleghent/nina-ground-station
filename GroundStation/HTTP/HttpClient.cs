#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
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

        [ImportingConstructor]
        public HttpClient() {
        }

        public HttpClient(HttpClient copyMe) : this() {
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
            var resolvedUri = Utilities.ResolveTokens(httpUri, this, true);

            try {
                if (HttpMethod == HttpMethodEnum.GET) {
                    response = await client.GetAsync(resolvedUri, ct);
                } else if (HttpMethod == HttpMethodEnum.POST) {
                    var body = HttpUtility.UrlEncode(Utilities.ResolveTokens(httpPostBody, this));
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
            return new HttpClient() {
                Icon = Icon,
                Name = Name,
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
                HttpPostBody = HttpPostBody,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(HttpClient)}";
        }

    }
}
