#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org> and contributors

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
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
        private readonly TTS tts;
        private string message;

        [ImportingConstructor]
        public SendToTTS() {
            tts = new TTS();
        }

        public SendToTTS(SendToTTS copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override object Clone() {
            return new SendToTTS(this) {
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
            var text = Utilities.Utilities.ResolveTokens(Message, this);

            await tts.Speak(text, ct);
        }

        public IList<string> Issues { get; set; } = new List<string>();

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(Message) || string.IsNullOrWhiteSpace(Message)) {
                i.Add("TTS message is missing");
            }

            if (!tts.HasVoice()) {
                i.Add("No Text-To-Speech voices found");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SendToTTS)}";
        }
    }
}