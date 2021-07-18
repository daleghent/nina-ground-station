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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace DaleGhent.NINA.GroundStation.SendToTelegram {

    [ExportMetadata("Name", "Send to Telegram")]
    [ExportMetadata("Description", "Sends a free form message to Telegram")]
    [ExportMetadata("Icon", "Telegram_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SendToTelegram : SequenceItem, IValidatable {
        private string message = string.Empty;
        private bool doNotNotify = false;

        [ImportingConstructor]
        public SendToTelegram() {
            TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
            TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public SendToTelegram(SendToTelegram copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Message {
            get => message;
            set {
                message = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public bool DoNotNotify {
            get => doNotNotify;
            set {
                doNotNotify = value;
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var bclient = new TelegramBotClient(TelegramAccessToken);

            Logger.Debug("SendToTelegram: Pushing message");

            try {
                await bclient.SendTextMessageAsync(TelegramChatId, Message, disableNotification: DoNotNotify, cancellationToken: ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to Telegram: {ex.Message}");
                throw ex;
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(TelegramAccessToken) || string.IsNullOrWhiteSpace(TelegramAccessToken)) {
                i.Add("Telegram bot access token is missing");
            }

            if (string.IsNullOrEmpty(TelegramChatId) || string.IsNullOrWhiteSpace(TelegramChatId)) {
                i.Add("Telegram chat ID missing");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new SendToTelegram() {
                Icon = Icon,
                Name = Name,
                Message = Message,
                DoNotNotify = DoNotNotify,
                Category = Category,
                Description = Description,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToTelegram)}";
        }

        private string TelegramAccessToken { get; set; }
        private string TelegramChatId { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TelegramAccessToken":
                    TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
                    break;
                case "TelegramChatId":
                    TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);
                    break;
            }
        }
    }
}