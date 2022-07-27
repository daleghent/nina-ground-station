using NINA.Core.Utility;
using NINA.Sequencer.Utility;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    internal class BackgroundQueueWorker<T> {
        private int queueSize;
        private CancellationTokenSource workerCts;
        private AsyncProducerConsumerQueue<T> messageQueue;
        private Func<T, CancellationToken, Task> workerFn;

        public BackgroundQueueWorker(int queueSize, Func<T, CancellationToken, Task> workerFn) {
            this.queueSize = queueSize;
            this.workerFn = workerFn;
        }

        public async Task Enqueue(T item) {
            if (messageQueue == null) { return; }
            await messageQueue.EnqueueAsync(item);
        }

        public void Stop() {
            try {
                // Cancel running worker
                workerCts?.Cancel();
                workerCts?.Dispose();
                messageQueue?.CompleteAdding();
            } catch (Exception) { }
        }

        public async Task Start() {
            try {
                Stop();
                workerCts = new CancellationTokenSource();
                messageQueue = new AsyncProducerConsumerQueue<T>(1000);
                while (await messageQueue.OutputAvailableAsync(workerCts.Token)) {
                    try {
                        var item = await messageQueue.DequeueAsync(workerCts.Token);

                        await workerFn(item, workerCts.Token);
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
    }
}