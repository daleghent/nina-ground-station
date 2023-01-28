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
            semaphore = new(initialCount: 1);
        }

        public async Task Enqueue(T item) {
            if (messageQueue == null) { return; }
            await messageQueue.EnqueueAsync(item);
        }

        public async void Stop() {
            try {
                // Wait a little for any last items to be enqueued, such as at the very end of a sequence
                await Task.Delay(TimeSpan.FromSeconds(5));

                // If a running WorkerFn() is taking more than 1 minute to complete, time out the attempt and finish shutting down
                await semaphore.WaitAsync(TimeSpan.FromMinutes(1));

                messageQueue?.CompleteAdding();
            } catch (Exception) {
            } finally {
                workerCts?.Cancel();
                workerCts?.Dispose();
                semaphore?.Release();
            }
        }

        public async Task Start() {
            try {
                workerCts = new CancellationTokenSource();
                messageQueue = new AsyncProducerConsumerQueue<T>(1000);
                while (await messageQueue.OutputAvailableAsync(workerCts.Token)) {
                    try {
                        Logger.Trace($"Message queue loop has awaken");
                        await semaphore.WaitAsync(workerCts.Token);
                        Logger.Trace($"Message queue loop acquired semaphore");

                        var item = await messageQueue.DequeueAsync(workerCts.Token);
                        await workerFn(item, workerCts.Token);

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