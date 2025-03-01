#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal class BackgroundQueueWorker<T> : IDisposable {
        private CancellationTokenSource workerCts;
        private AsyncProducerConsumerQueue<T> messageQueue;
        private readonly Func<T, CancellationToken, Task> workerFn;

        public BackgroundQueueWorker(Func<T, CancellationToken, Task> workerFn) {
            this.workerFn = workerFn;
        }

        public async Task Enqueue(T item) {
            var localCopy = messageQueue;
            if (localCopy == null) { return; }
            await localCopy.EnqueueAsync(item);
        }

        public async Task Stop() {
            try {
                // use a local copy of the current message queue to prevent case where Start() could be run prior to the delays passing and setting a fresh queue
                var localCopy = messageQueue;
                // Wait a little for any last items to be enqueued, such as at the very end of a sequence
                await Task.Delay(TimeSpan.FromSeconds(5));

                Logger.Trace("Complete adding to queue");
                localCopy?.CompleteAdding();
            } catch (Exception) {
            } finally {
                try {
                    workerCts?.Cancel();
                    workerCts?.Dispose();
                } catch {
                }
            }
        }

        public void Start() {
            workerCts = new CancellationTokenSource();
            messageQueue = new AsyncProducerConsumerQueue<T>(1000);
            // Start the work in background. The inside method uses local copies of the class fields to prevent race conditions
            _ = DoWork(messageQueue, workerCts.Token);
        }

        private async Task DoWork(AsyncProducerConsumerQueue<T> queue, CancellationToken token) {
            try {
                while (await queue.OutputAvailableAsync(token)) {
                    try {
                        Logger.Trace($"Message queue loop has awoken");
                        var item = await queue.DequeueAsync(token);
                        await workerFn(item, token);
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    }
                }
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public void Dispose() {
            GC.SuppressFinalize(this);
        }
    }
}