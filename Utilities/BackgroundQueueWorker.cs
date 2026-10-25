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
        // Queue, CTS, and worker task are swapped as one unit so Start(), Stop(), and Dispose() never see a mix of old and new state
        private WorkerState state;
        private readonly Func<T, CancellationToken, Task> workerFn;

        public BackgroundQueueWorker(Func<T, CancellationToken, Task> workerFn) {
            this.workerFn = workerFn;
        }

        public async Task Enqueue(T item) {
            var localState = Volatile.Read(ref state);
            if (localState == null) { return; }

            try {
                await localState.Queue.EnqueueAsync(item);
            } catch (InvalidOperationException) {
                // Queue was completed between the null check and EnqueueAsync
            }
        }

        public async Task Stop() {
            var localState = Volatile.Read(ref state);
            if (localState == null) { return; }

            // Wait a little for any last items to be enqueued, such as at the very end of a sequence.
            // The queue is left in place during this time so those items are still accepted.
            await Task.Delay(TimeSpan.FromSeconds(5));

            // If a concurrent Start() or Stop() has already swapped out this state, that caller owns its shutdown
            if (Interlocked.CompareExchange(ref state, null, localState) != localState) { return; }

            await localState.Shutdown();
        }

        public void Start() {
            var queue = new AsyncProducerConsumerQueue<T>(1000);
            var cts = new CancellationTokenSource();

            // Start the work in background. The inside method uses local copies of the state to prevent race conditions
            var newState = new WorkerState(queue, cts, DoWork(queue, cts.Token));

            // Atomically swap in the new state. Any prior worker is left to drain its queue in the background
            var oldState = Interlocked.Exchange(ref state, newState);
            if (oldState != null) {
                _ = oldState.Shutdown();
            }
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
            var localState = Interlocked.Exchange(ref state, null);

            try {
                localState?.Queue.CompleteAdding();
                localState?.Cts.Cancel();
                localState?.Cts.Dispose();
            } catch { }

            GC.SuppressFinalize(this);
        }

        private sealed class WorkerState(AsyncProducerConsumerQueue<T> queue, CancellationTokenSource cts, Task workerTask) {
            public AsyncProducerConsumerQueue<T> Queue { get; } = queue;
            public CancellationTokenSource Cts { get; } = cts;

            public async Task Shutdown() {
                try {
                    Logger.Trace("Complete adding to queue");
                    Queue.CompleteAdding();

                    // Allow the worker to drain remaining items before cancelling
                    await Task.WhenAny(workerTask, Task.Delay(TimeSpan.FromSeconds(10)));
                } catch (Exception) {
                } finally {
                    try {
                        Cts.Cancel();
                        Cts.Dispose();
                    } catch {
                    }
                }
            }
        }
    }
}