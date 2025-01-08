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
        private readonly int queueSize;
        private CancellationTokenSource workerCts;
        private AsyncProducerConsumerQueue<T> messageQueue;
        private Func<T, CancellationToken, Task> workerFn;
        private readonly SemaphoreSlim semaphore;

        public BackgroundQueueWorker(int queueSize, Func<T, CancellationToken, Task> workerFn) {
            this.queueSize = queueSize;
            this.workerFn = workerFn;
            semaphore = new(initialCount: 1, maxCount: 1);
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

                // If a running WorkerFn() is taking more than 1 minute to complete, time out the attempt and finish shutting down
                await semaphore.WaitAsync(TimeSpan.FromMinutes(1));

                Logger.Trace("Complete adding to queue");
                localCopy?.CompleteAdding();
            } catch (Exception) {
            } finally {
                try {
                    workerCts?.Cancel();
                    workerCts?.Dispose();
                } catch {
                } finally {
                    semaphore?.Release();
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
                        Logger.Trace($"Message queue loop has awaken");
                        await semaphore.WaitAsync(token);
                        Logger.Trace($"Message queue loop acquired semaphore");

                        var item = await queue.DequeueAsync(token);
                        await workerFn(item, token);

                        semaphore.Release();
                        Logger.Trace($"Message queue loop released semaphore");
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception ex) {
                        Logger.Error(ex);
                    } finally {
                        semaphore?.Release();
                    }
                }
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                Logger.Error(ex);
            }
        }

        public void Dispose() {
            semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}