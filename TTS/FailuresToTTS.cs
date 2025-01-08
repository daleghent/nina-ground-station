#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.MetadataClient;
using DaleGhent.NINA.GroundStation.Utilities;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.TTS {

    [ExportMetadata("Name", "Failures to TTS")]
    [ExportMetadata("Description", "Plays a Text-To-Speech announcement when a sequence instruction fails")]
    [ExportMetadata("Icon", "TTS_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToTTS : SequenceTrigger, IValidatable, IDisposable {
        private ISequenceRootContainer failureHook;
        private readonly BackgroundQueueWorker<SequenceEntityFailureEventArgs> queueWorker;

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
        public FailuresToTTS(ICameraMediator cameraMediator,
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

            queueWorker = new BackgroundQueueWorker<SequenceEntityFailureEventArgs>(1000, WorkerFn);
        }

        public FailuresToTTS(FailuresToTTS copyMe) : this(cameraMediator: copyMe.cameraMediator,
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
            return new FailuresToTTS(this) {
            };
        }

        public override void Initialize() {
            queueWorker.Start();
        }

        public override void Teardown() {
            queueWorker.Stop();
        }

        public void Dispose() {
            queueWorker.Dispose();
            GC.SuppressFinalize(this);
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
                queueWorker.Start();
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
            var text = Utilities.Utilities.ResolveTokens(GroundStation.GroundStationConfig.TTSFailureMessage, item.Entity, metadata);
            text = Utilities.Utilities.ResolveFailureTokens(text, failedItem);

            Logger.Info($"{Name}: Speaking \"{text}\"");
            using var tts = new TextToSpeech();
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
            using var tts = new TextToSpeech();

            if (!tts.HasVoice()) {
                i.Add("No Text-To-Speech voices found");
            }

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged(nameof(Issues));
            }

            return i.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}