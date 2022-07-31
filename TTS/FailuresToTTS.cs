using DaleGhent.NINA.GroundStation.Utilities;
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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DaleGhent.NINA.GroundStation.TTS {

    [ExportMetadata("Name", "Failures to TTS")]
    [ExportMetadata("Description", "Plays a Text-To-Speech announcement when a sequence instruction fails")]
    [ExportMetadata("Icon", "TTS_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToTTS : SequenceTrigger, IValidatable {
        private TTS tts;
        private ISequenceRootContainer failureHook;
        private BackgroundQueueWorker<SequenceEntityFailureEventArgs> queueWorker;

        [ImportingConstructor]
        public FailuresToTTS() {
            tts = new TTS();
            queueWorker = new BackgroundQueueWorker<SequenceEntityFailureEventArgs>(1000, WorkerFn);

            TTSFailureMessage = Properties.Settings.Default.TTSFailureMessage;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToTTS(FailuresToTTS copyMe) : this() {
            CopyMetaData(copyMe);
        }

        public override object Clone() {
            return new FailuresToTTS(this) {
            };
        }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TTSFailureMessage":
                    TTSFailureMessage = Properties.Settings.Default.TTSFailureMessage;
                    break;
            }
        }

        private string TTSFailureMessage { get; set; }

        public override void Initialize() {
            queueWorker.Stop();
            _ = queueWorker.Start();
        }

        public override void Teardown() {
            queueWorker.Stop();
        }

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null && failureHook != null) {
                // When trigger is removed from sequence, unregister event handler
                // This could potentially be skipped by just using weak events instead
                failureHook.FailureEvent -= Root_FailureEvent;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                queueWorker.Stop();
                // When dragging the item into the sequence while the sequence is already running
                // Make sure to register the event handler as "SequenceBlockInitialized" is already done
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
                _ = queueWorker.Start();
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

            if (arg2.Entity is FailuresToTTS /*|| arg2.Entity is SendToTTS*/) {
                // Prevent email items to send email failures
                return;
            }

            await queueWorker.Enqueue(arg2);
        }

        private async Task WorkerFn(SequenceEntityFailureEventArgs item, CancellationToken token) {
            var failedItem = FailedItem.FromEntity(item.Entity, item.Exception);
            var text = Utilities.Utilities.ResolveTokens(TTSFailureMessage, item.Entity);
            text = Utilities.Utilities.ResolveFailureTokens(text, failedItem);

            await tts.Speak(text, token);
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

        public IList<string> Issues { get; set; } = new List<string>();

        public bool Validate() {
            var i = new List<string>();

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
            return $"Category: {Category}, Item: {nameof(FailuresToTTS)}";
        }
    }
}