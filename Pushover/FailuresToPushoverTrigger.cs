#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Pushover;
using DaleGhent.NINA.GroundStation.Utilities;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using NINA.Sequencer.Utility;
using PushoverClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace DaleGhent.NINA.GroundStation.FailuresToPushoverTrigger {

    [ExportMetadata("Name", "Failures to Pushover")]
    [ExportMetadata("Description", "Sends an event to Pushover when a sequence instruction fails")]
    [ExportMetadata("Icon", "Pushover_SVG")]
    [ExportMetadata("Category", "Ground Station")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FailuresToPushoverTrigger : SequenceTrigger, IValidatable {
        private PushoverCommon pushover;
        private Priority priority;
        private NotificationSound notificationSound;

        private CancellationTokenSource workerCts;
        private AsyncProducerConsumerQueue<SequenceEntityFailureEventArgs> messageQueue;

        [ImportingConstructor]
        public FailuresToPushoverTrigger() {
            pushover = new PushoverCommon();

            PushoverFailureTitleText = Properties.Settings.Default.PushoverFailureTitleText;
            PushoverFailureBodyText = Properties.Settings.Default.PushoverFailureBodyText;
            NotificationSound = Properties.Settings.Default.PushoverDefaultFailureSound;
            Priority = Properties.Settings.Default.PushoverDefaultFailurePriority;

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public FailuresToPushoverTrigger(FailuresToPushoverTrigger copyMe) : this() {
            CopyMetaData(copyMe);
        }

        private ISequenceRootContainer failureHook;

        public override void Initialize() {
            workerCts = new CancellationTokenSource();
            _ = RunMessageQueueWorker();
        }

        public override void Teardown() {
            try {
                // Cancel running worker
                workerCts?.Dispose();
                messageQueue.CompleteAdding();
            } catch (Exception) { }
        }

        public override void AfterParentChanged() {
            var root = ItemUtility.GetRootContainer(this.Parent);
            if (root == null && failureHook != null) {
                // When trigger is removed from sequence, unregister event handler
                // This could potentially be skipped by just using weak events instead
                failureHook.FailureEvent -= Root_FailureEvent;
                failureHook = null;
            } else if (root != null && root != failureHook && this.Parent.Status == SequenceEntityStatus.RUNNING) {
                try {
                    // Cancel running worker
                    workerCts?.Dispose();
                } catch (Exception) { }
                // When dragging the item into the sequence while the sequence is already running
                // Make sure to register the event handler as "SequenceBlockInitialized" is already done
                failureHook = root;
                failureHook.FailureEvent += Root_FailureEvent;
                workerCts = new CancellationTokenSource();
                _ = RunMessageQueueWorker();
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

            if (arg2.Entity is FailuresToPushoverTrigger || arg2.Entity is SendToPushover.SendToPushover) {
                // Prevent pushover items to send pushover failures
                return;
            }

            await messageQueue.EnqueueAsync(arg2);
        }

        private async Task RunMessageQueueWorker() {
            try {
                messageQueue = new AsyncProducerConsumerQueue<SequenceEntityFailureEventArgs>(1000);
                while (await messageQueue.OutputAvailableAsync(workerCts.Token)) {
                    try {
                        var item = await messageQueue.DequeueAsync(workerCts.Token);

                        var failedItem = FailedItem.FromEntity(item.Entity, item.Exception);

                        var title = Utilities.Utilities.ResolveTokens(PushoverFailureTitleText, item.Entity);
                        var message = Utilities.Utilities.ResolveTokens(PushoverFailureBodyText, item.Entity);

                        title = Utilities.Utilities.ResolveFailureTokens(title, failedItem);
                        message = Utilities.Utilities.ResolveFailureTokens(message, failedItem);

                        var attempts = 3; // Todo: Make it configurable?
                        for (int i = 0; i < attempts; i++) {
                            try {
                                var newCts = CancellationTokenSource.CreateLinkedTokenSource(workerCts.Token);
                                using (workerCts.Token.Register(() => newCts.CancelAfter(TimeSpan.FromSeconds(Utilities.Utilities.cancelTimeout)))) {
                                    workerCts.Token.ThrowIfCancellationRequested();
                                    await pushover.PushMessage(title, message, Priority, NotificationSound, newCts.Token);
                                    // When successful break the retry loop
                                    break;
                                }
                            } catch (Exception ex) {
                                Logger.Error($"Pushover failed to send message. Attempt {i + 1}/{attempts}", ex);
                            }
                        }
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    }
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        [JsonProperty]
        public Priority Priority {
            get => priority;
            set {
                priority = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public NotificationSound NotificationSound {
            get => notificationSound;
            set {
                notificationSound = value;
                RaisePropertyChanged();
            }
        }

        public Priority[] Priorities => Enum.GetValues(typeof(Priority)).Cast<Priority>().Where(p => p != Priority.Emergency).ToArray();
        public NotificationSound[] NotificationSounds => Enum.GetValues(typeof(NotificationSound)).Cast<NotificationSound>().Where(p => p != NotificationSound.NotSet).ToArray();

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
            var i = new List<string>(pushover.ValidateSettings());

            if (i != Issues) {
                Issues = i;
                RaisePropertyChanged("Issues");
            }

            return i.Count == 0;
        }

        public override object Clone() {
            return new FailuresToPushoverTrigger(this) {
                Priority = Priority,
                NotificationSound = NotificationSound,
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FailuresToPushoverTrigger)}";
        }

        private string PushoverFailureTitleText { get; set; }
        private string PushoverFailureBodyText { get; set; }

        private void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverFailureTitleText":
                    PushoverFailureTitleText = Properties.Settings.Default.PushoverFailureTitleText;
                    break;

                case "PushoverFailureBodyText":
                    PushoverFailureBodyText = Properties.Settings.Default.PushoverFailureBodyText;
                    break;
            }
        }
    }
}