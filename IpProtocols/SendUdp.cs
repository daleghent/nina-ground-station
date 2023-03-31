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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static DaleGhent.NINA.GroundStation.IpProtocols.IpCommon;

namespace DaleGhent.NINA.GroundStation.IpProtocols {

    [ExportMetadata("Name", "Send UDP")]
    [ExportMetadata("Description", "Emits a UDP packet to the specified endpoint containing the provided payload")]
    [ExportMetadata("Icon", "PlaySoundSVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class SendUdp : SequenceItem, IValidatable, INotifyPropertyChanged {
        private string address = string.Empty;
        private ushort port = 0;
        private string payload = string.Empty;
        private bool addressIsOk = true;
        private short payloadType;
        private short lineTermination;

        private const ushort maxPlayloadLen = 65507;

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
        public SendUdp(ICameraMediator cameraMediator,
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

            metadata = new Metadata(cameraMediator, domeMediator,
                filterWheelMediator, flatDeviceMediator, focuserMediator,
                guiderMediator, rotatorMediator, safetyMonitorMediator,
                switchMediator, telescopeMediator, weatherDataMediator);

            payloadType = Convert.ToInt16(IpCommon.PayloadType.ASCII);
            lineTermination = Convert.ToInt16(IpCommon.LineTermination.CRLF);

            PropertyChanged += OnPropertyChanged;
        }

        public SendUdp(SendUdp copyMe) : this(cameraMediator: copyMe.cameraMediator,
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
            return new SendUdp(this) {
                Address = Address,
                Port = Port,
                Payload = Payload,
                PayloadType = PayloadType,
                LineTermination = LineTermination,
            };
        }

        [JsonProperty]
        public string Address {
            get => address;
            set {
                address = value;
                Validate();
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public ushort Port {
            get => port;
            set {
                port = value;
                Validate();
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Payload {
            get => payload;
            set {
                payload = value;

                if (payloadType == (short)IpCommon.PayloadType.ASCII) {
                    PayloadBytes = Encoding.ASCII.GetBytes(value);
                }

                Validate();
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public short PayloadType {
            get => payloadType;
            set {
                payloadType = value;

                if (payloadType == (short)IpCommon.PayloadType.Binary) {
                    Validate();
                }

                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public short LineTermination {
            get => lineTermination;
            set {
                lineTermination = value;
                RaisePropertyChanged();
            }
        }

        private byte[] PayloadBytes { get; set; } = Array.Empty<byte>();
        private IPAddress IPAddressInfo { get; set; }

        private bool AddressIsOK {
            get => addressIsOk;
            set {
                addressIsOk = value;
                Validate();
            }
        }

        public static PayloadType[] PayloadTypes => Enum.GetValues(typeof(PayloadType)).Cast<PayloadType>().ToArray();
        public static LineTermination[] LineTerminations => Enum.GetValues(typeof(LineTermination)).Cast<LineTermination>().ToArray();

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            try {
                byte[] sendbuf;

                if (payloadType == (short)IpCommon.PayloadType.ASCII) {
                    var payload = Utilities.Utilities.ResolveTokens(Payload, this, metadata);

                    if (lineTermination == (short)IpCommon.LineTermination.CR) {
                        payload = payload.Replace(Environment.NewLine, "\r");

                        if (!payload.EndsWith("\r")) {
                            payload += '\r';
                        }
                    } else if (lineTermination == (short)IpCommon.LineTermination.LF) {
                        payload = payload.Replace(Environment.NewLine, "\n");

                        if (!payload.EndsWith("\n")) {
                            payload += '\n';
                        }
                    } else if (lineTermination == (short)IpCommon.LineTermination.CRLF) {
                        payload = payload.Replace(Environment.NewLine, "\r\n");

                        if (!payload.EndsWith("\r\n")) {
                            payload += "\r\n";
                        }
                    } else if (lineTermination == (short)IpCommon.LineTermination.None) {
                        payload = payload.Replace(Environment.NewLine, string.Empty);
                    }

                    sendbuf = Encoding.ASCII.GetBytes(payload);
                } else if (payloadType == (short)IpCommon.PayloadType.Binary) {
                    string[] byteStrs = Payload.Trim().Split(' ');

                    sendbuf = new byte[byteStrs.Length];
                    int i = 0;

                    foreach (string str in byteStrs) {
                        sendbuf[i] = byte.Parse(str, NumberStyles.HexNumber);
                        i++;
                    }
                } else {
                    throw new SequenceEntityFailedException($"Unknown payload type {payloadType}");
                }

                using var udp = new UdpClient((AddressFamily)(IPAddressInfo?.AddressFamily)) {
                    DontFragment = true,
                };

                udp.Connect(IPAddressInfo.ToString(), port);
                var response = await udp.SendAsync(sendbuf, ct);

                Logger.Debug($"{response} bytes sent to {Address}:{Port}");
                sendbuf = null;
            } catch (Exception ex) {
                throw new SequenceEntityFailedException($"{ex.GetType()}: {ex.Message}");
            }

            return;
        }

        public IList<string> Issues { get; set; } = new List<string>();

        public bool Validate() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(Address) || string.IsNullOrWhiteSpace(Address)) {
                issues.Add("No hostname or IP address specified");
            } else if (!AddressIsOK) {
                issues.Add("IP or hostname is invalid");
            }

            if (port < 1 || port > ushort.MaxValue) {
                issues.Add("Invalid port number");
            }

            if (payloadType == (short)IpCommon.PayloadType.Binary) {
                try {
                    string[] byteStrs = payload.Trim().Split(' ');

                    var buf = new byte[byteStrs.Length];
                    int i = 0;

                    foreach (string str in byteStrs) {
                        if (!HexRegex().Match(str).Success) { throw new FormatException($"Invalid byte {str}"); }

                        buf[i] = byte.Parse(str, NumberStyles.HexNumber);
                        i++;
                    }
                } catch {
                    issues.Add("Invalid payload format for a Binary payload type");
                }
            }

            if (PayloadBytes.Length > maxPlayloadLen) {
                issues.Add($"Payload is larger than {maxPlayloadLen} bytes");
            }

            if (issues != Issues) {
                Issues = issues;
                RaisePropertyChanged(nameof(Issues));
            }

            return issues.Count == 0;
        }

        private async void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName.Equals(nameof(Address))) {
                if (!string.IsNullOrEmpty(Address) && !string.IsNullOrWhiteSpace(Address)) {
                    Logger.Trace($"Checking IP format and DNS for {Address}");

                    if (!IPAddress.TryParse(Address, out IPAddress addrinfo)) {
                        try {
                            var iphostentry = await Dns.GetHostEntryAsync(Address);
                            IPAddressInfo = iphostentry.AddressList.FirstOrDefault();

                            // We have a good DNS lookup
                            Logger.Trace($"DNS lookup succeeded for {iphostentry.HostName}");

                            AddressIsOK = true;
                            return;
                        } catch (SocketException) {
                            // It failed IP address parsing and DNS lookup. No good.
                            AddressIsOK = false;
                            return;
                        }
                    } else {
                        // We have a valid IP address
                        Logger.Trace($"IP address parse succeeded for {Address}");

                        IPAddressInfo = addrinfo;
                        AddressIsOK = true;
                    }
                } else {
                    // We have an empty Address
                    AddressIsOK = false;
                }
            }
        }

        public override void Teardown() {
            PropertyChanged -= OnPropertyChanged;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendUdp)}, Endpoint: {Address}:{Port}, Payload Type: {(PayloadType)payloadType}";
        }

        [GeneratedRegex("^[0-9a-f]{2}$", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex HexRegex();
    }
}