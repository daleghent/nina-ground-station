#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Utilities {

    public class BackgroundQueueWorker<T> {
        private readonly Channel<Func<T, CancellationToken, Task>> _channel;
        private readonly Task _backgroundTask;
        private readonly CancellationTokenSource _cts = new();

        public BackgroundQueueWorker(int capacity = 100) {
            // Initialize a bounded channel with the specified capacity
            _channel = Channel.CreateBounded<Func<T, CancellationToken, Task>>(new BoundedChannelOptions(capacity) {
                FullMode = BoundedChannelFullMode.Wait // Block enqueue when full
            });

            // Start processing tasks in the background
            _backgroundTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        // Enqueue a work item
        public async Task AddItemAsync(Func<T, CancellationToken, Task> workItem) {
            ArgumentNullException.ThrowIfNull(workItem);

            try {
                await _channel.Writer.WriteAsync(workItem, _cts.Token);
            } catch (ChannelClosedException) {
                throw new InvalidOperationException("Queue is no longer accepting tasks.");
            }
        }

        // Process queued tasks
        private async Task ProcessQueueAsync(CancellationToken cancellationToken) {
            try {
                while (await _channel.Reader.WaitToReadAsync(cancellationToken)) {
                    while (_channel.Reader.TryRead(out var workItem)) {
                        try {
                            await workItem(default(T), cancellationToken);
                        } catch (Exception ex) {
                            Logger.Error($"Task execution failed: {ex}");
                        }
                    }
                }
            } catch (OperationCanceledException) {
                // Graceful shutdown
            }
        }

        // Gracefully shut down the worker
        public async Task ShutdownAsync() {
            _channel.Writer.Complete(); // Signal no more tasks will be enqueued
            _cts.Cancel(); // Cancel any in-progress tasks
            await _backgroundTask; // Wait for all processing to complete
        }
    }
}