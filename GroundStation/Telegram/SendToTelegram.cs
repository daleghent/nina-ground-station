#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Telegram;
using Newtonsoft.Json;
using NINA.Core.Model;
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
    public class SendToTelegram : SequenceItem, IValidatable {
        private TelegramCommon telegram;
        private string message = string.Empty;
        private bool doNotNotify = false;

        [ImportingConstructor]
        public SendToTelegram() {
            telegram = new TelegramCommon();
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
            await telegram.SendTelegram(Message, DoNotNotify, ct);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public bool Validate() {
            var i = new List<string>(telegram.ValidateSettings());

            if (string.IsNullOrEmpty(Message) || string.IsNullOrWhiteSpace(Message)) {
                i.Add("Telegram message is empty!");
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
    }
}