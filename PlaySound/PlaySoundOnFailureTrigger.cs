#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using DaleGhent.NINA.GroundStation.PlaySound;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.PlaySoundOnFailureTrigger {

    [ExportMetadata("Name", "Play Sound On Failure")]
    [ExportMetadata("Description", "Plays the specified sound when a sequence instruction fails")]
    [ExportMetadata("Icon", "PlaySound_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public partial class PlaySoundOnFailureTrigger : SequenceTrigger, IValidatable {
        private string soundFile = string.Empty;
        private ISequenceRootContainer failureHook;

        [ImportingConstructor]
        public PlaySoundOnFailureTrigger() {
            SoundFile = GroundStation.GroundStationConfig.PlaySoundDefaultFailureFile;

            Validate();
        }

        public PlaySoundOnFailureTrigger(PlaySoundOnFailureTrigger copyMe) : this() {
            CopyMetaData(copyMe);
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

        public string SoundFileShort => Path.GetFileName(soundFile);

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null && failureHook != null) {
                // When trigger is removed from sequence, unregister event handler
                // This could potentially be skipped by just using weak events instead
                failureHook.FailureEvent -= Root_FailureEvent;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                // When dragging the item into the sequence while the sequence is already running
                // Make sure to register the event handler as "SequenceBlockInitialized" is already done
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
            }
            base.AfterParentChanged();
        }

        public override void SequenceBlockInitialize() {
            // Register failure event when the parent context starts
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent += Root_FailureEvent;
            }
            base.SequenceBlockInitialize();
        }

        public override void SequenceBlockTeardown() {
            // Unregister failure event when the parent context ends
            failureHook = ItemUtility.GetRootContainer(this.Parent);
            if (failureHook != null) {
                failureHook.FailureEvent -= Root_FailureEvent;
            }
        }

        private async Task Root_FailureEvent(object arg1, SequenceEntityFailureEventArgs arg2) {
            if (arg2.Entity == null) {
                // An exception without context has occurred. Not sure when this can happen
                // Todo: Might be worthwile to send in a different style
                return;
            }

            if (arg2.Entity is PlaySoundOnFailureTrigger) {
                return;
            }

            var playSoundCommon = new PlaySoundCommon() {
                SoundFile = soundFile,
            };

            await playSoundCommon.PlaySound(CancellationToken.None);
        }

        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken ct) {
            return Task.CompletedTask;
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            return false;
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

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

        public override object Clone() {
            return new PlaySoundOnFailureTrigger(this) {
                SoundFile = SoundFile,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}, SoundFile: {soundFile}";
        }

        [RelayCommand]
        internal void OpenSelectSoundFileDialog(object obj) {
            Microsoft.Win32.OpenFileDialog dialog = new() {
                FileName = string.Empty,
                Filter = PlaySound.PlaySoundCommon.FileTypeFilter,
            };

            if (dialog.ShowDialog() == true) {
                SoundFile = dialog.FileName;
            }
        }
    }
}