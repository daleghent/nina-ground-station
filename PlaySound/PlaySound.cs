#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.PlaySound {

    [ExportMetadata("Name", "Play Sound")]
    [ExportMetadata("Description", "Plays the specified audio file")]
    [ExportMetadata("Icon", "PlaySound_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class PlaySound : SequenceItem, IValidatable {
        private readonly PlaySoundCommon playSoundCommon;
        private string soundFile = string.Empty;
        private bool waitUntilFinished = true;

        [ImportingConstructor]
        public PlaySound() {
            SoundFile = GroundStation.GroundStationConfig.PlaySoundDefaultFile;

            Validate();
        }

        public PlaySound(PlaySound copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override object Clone() {
            return new PlaySound(this) {
                SoundFile = SoundFile,
                WaitUntilFinished = WaitUntilFinished,
            };
        }

        [JsonProperty]
        public string SoundFile {
            get => soundFile;
            set {
                soundFile = value;
                Validate();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SoundFileShort));
            }
        }

        [JsonProperty]
        public bool WaitUntilFinished {
            get => waitUntilFinished;
            set {
                waitUntilFinished = value;
                RaisePropertyChanged();
            }
        }

        public string SoundFileShort => Path.GetFileName(soundFile);

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken ct) {
            var playSoundCommon = new PlaySoundCommon() {
                SoundFile = soundFile,
                WaitUntilFinished = waitUntilFinished,
            };

            await playSoundCommon.PlaySound(ct);

            return;
        }

        public IList<string> Issues { get; set; } = [];

        public bool Validate() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(soundFile)) {
                i.Add("Sound file has not been specified");
            } else {
                if (!File.Exists(soundFile)) {
                    i.Add("Sound file does not exist");
                }
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, SoundFile: {soundFile}, WaitUntilFinished: {WaitUntilFinished}";
        }

        [RelayCommand]
        internal void OpenSelectSoundFileDialog(object obj) {
            Microsoft.Win32.OpenFileDialog dialog = new() {
                FileName = string.Empty,
                Filter = PlaySoundCommon.FileTypeFilter,
            };

            if (dialog.ShowDialog() == true) {
                SoundFile = dialog.FileName;
            }
        }
    }
}