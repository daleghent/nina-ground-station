#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using PushoverClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Pushover {
    public class PushoverCommon {

        public PushoverCommon() {
            PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
            PushoverUserKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task PushMessage(string title, string message, Priority priority, NotificationSound notificationSound, CancellationToken ct) {
            var pclient = new PushoverClient.Pushover(PushoverAppKey, PushoverUserKey);

            Logger.Debug("Pushing message");

            try {
                if (ct.IsCancellationRequested) {
                    Logger.Info("Push cancelled");
                    return;
                }

                var response = await pclient.PushAsync(title, message, priority: priority, notificationSound: notificationSound);

                if (response.Status != 1 || response.Errors?.Count > 0) {
                    throw new Exception($"Push failed. Status={response.Status}, Errors={response.Errors.Select(array => string.Join(", ", array))}");
                }
            } catch (Exception ex) {
                Logger.Error($"Error sending to Pushover: {ex.Message}");
                throw ex;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(PushoverAppKey) || string.IsNullOrWhiteSpace(PushoverAppKey)) {
                issues.Add("Pushover app key is missing");
            }

            if (string.IsNullOrEmpty(PushoverUserKey) || string.IsNullOrWhiteSpace(PushoverUserKey)) {
                issues.Add("Pushover user key is missing");
            }

            return issues;
        }

        private string PushoverAppKey { get; set; }
        private string PushoverUserKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "PushoverAppKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverAppKey);
                    break;
                case "PushoverUserKey":
                    PushoverAppKey = Security.Decrypt(Properties.Settings.Default.PushoverUserKey);
                    break;
            }
        }
    }
}
