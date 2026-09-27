#region "copyright"

/*
    Copyright (c) 2021-2026 Dale Ghent <daleg@elemental.org>

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
        private Task workerTask;
        private readonly Func<T, CancellationToken, Task> workerFn;

        public BackgroundQueueWorker(Func<T, CancellationToken, Task> workerFn) {
            this.workerFn = workerFn;
        }

        public async Task Enqueue(T item) {
            var localCopy = Volatile.Read(ref messageQueue);
            if (localCopy == null) { return; }

            try {
                await localCopy.EnqueueAsync(item);
            } catch (InvalidOperationException) {
                // Queue was completed between the null check and EnqueueAsync
            }
        }

        public async Task Stop() {
            // Atomically capture and clear all state so a concurrent Start() won't conflict
            var localQueue = Interlocked.Exchange(ref messageQueue, null);
            var localCts = Interlocked.Exchange(ref workerCts, null);
            var localTask = Interlocked.Exchange(ref workerTask, null);

            try {
                // Wait a little for any last items to be enqueued, such as at the very end of a sequence
                await Task.Delay(TimeSpan.FromSeconds(5));

                Logger.Trace("Complete adding to queue");
                localQueue?.CompleteAdding();

                // Allow the worker to drain remaining items before cancelling
                if (localTask != null) {
                    await Task.WhenAny(localTask, Task.Delay(TimeSpan.FromSeconds(10)));
                }
            } catch (Exception) {
            } finally {
                try {
                    localCts?.Cancel();
                    localCts?.Dispose();
                } catch {
                }
            }
        }

        public void Start() {
            var newCts = new CancellationTokenSource();
            var newQueue = new AsyncProducerConsumerQueue<T>(1000);

            // Atomically swap in the new state, capturing any prior state for cleanup
            var oldCts = Interlocked.Exchange(ref workerCts, newCts);
            var oldQueue = Interlocked.Exchange(ref messageQueue, newQueue);

            try { oldQueue?.CompleteAdding(); } catch { }
            try {
                oldCts?.Cancel();
                oldCts?.Dispose();
            } catch { }

            // Start the work in background. The inside method uses local copies of the class fields to prevent race conditions
            workerTask = DoWork(newQueue, newCts.Token);
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
            try {
                var cts = Interlocked.Exchange(ref workerCts, null);
                Interlocked.Exchange(ref messageQueue, null)?.CompleteAdding();
                Interlocked.Exchange(ref workerTask, null);
                cts?.Cancel();
                cts?.Dispose();
            } catch { }

            GC.SuppressFinalize(this);
        }
    }
}