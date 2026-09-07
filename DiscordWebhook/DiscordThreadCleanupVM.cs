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
using System.Text;
using System.Threading;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {

    public class DiscordThreadCleanupVM : BaseINPC, IDisposable {
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly StringBuilder logBuilder = new();

        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        private string statusText = "Starting...";

        public string StatusText {
            get => statusText;
            private set {
                statusText = value;
                RaisePropertyChanged();
            }
        }

        public string LogText => logBuilder.ToString();

        private int progressValue;

        public int ProgressValue {
            get => progressValue;
            private set {
                progressValue = value;
                RaisePropertyChanged();
            }
        }

        private int progressMax = 1;

        public int ProgressMax {
            get => progressMax;
            private set {
                progressMax = value;
                RaisePropertyChanged();
            }
        }

        private bool isIndeterminate = true;

        public bool IsIndeterminate {
            get => isIndeterminate;
            private set {
                isIndeterminate = value;
                RaisePropertyChanged();
            }
        }

        private bool isRunning = true;

        public bool IsRunning {
            get => isRunning;
            private set {
                isRunning = value;
                RaisePropertyChanged();
            }
        }

        private bool isFinished;

        public bool IsFinished {
            get => isFinished;
            private set {
                isFinished = value;
                RaisePropertyChanged();
            }
        }

        // Invoked on the UI thread via Progress<T>.
        public void ReportProgress(ThreadCleanupProgress progress) {
            if (!string.IsNullOrEmpty(progress.LogLine)) {
                logBuilder.AppendLine(progress.LogLine);
                RaisePropertyChanged(nameof(LogText));
            }

            if (progress.TotalCount > 0) {
                IsIndeterminate = false;
                ProgressMax = progress.TotalCount;
                ProgressValue = progress.CompletedCount;

                if (progress.CompletedCount == 0) {
                    StatusText = progress.LogLine; // "Found N session thread(s) to delete."
                } else if (progress.CompletedCount < progress.TotalCount && progress.EstimatedTimeRemaining.HasValue) {
                    StatusText = $"Deleting thread {progress.CompletedCount + 1} of {progress.TotalCount} — about {FormatTimeRemaining(progress.EstimatedTimeRemaining.Value)} remaining";
                } else {
                    StatusText = $"Deleted {progress.CompletedCount} of {progress.TotalCount} thread(s)";
                }
            } else if (!string.IsNullOrEmpty(progress.LogLine)) {
                StatusText = progress.LogLine;
            }
        }

        public void Complete(string finalStatus) {
            IsRunning = false;
            IsFinished = true;
            IsIndeterminate = false;

            if (!string.IsNullOrEmpty(finalStatus)) {
                StatusText = finalStatus;
                logBuilder.AppendLine(finalStatus);
                RaisePropertyChanged(nameof(LogText));
            }
        }

        public void RequestCancel() {
            if (!IsRunning) {
                return;
            }

            StatusText = "Canceling...";
            cancellationTokenSource.Cancel();
        }

        private static string FormatTimeRemaining(TimeSpan timeRemaining) {
            if (timeRemaining.TotalHours >= 1) {
                return $"{(int)timeRemaining.TotalHours}h {timeRemaining.Minutes}m";
            }

            if (timeRemaining.TotalMinutes >= 1) {
                return $"{timeRemaining.Minutes}m {timeRemaining.Seconds}s";
            }

            return $"{Math.Max(timeRemaining.Seconds, 1)}s";
        }

        public void Dispose() {
            cancellationTokenSource.Dispose();
        }
    }
}
