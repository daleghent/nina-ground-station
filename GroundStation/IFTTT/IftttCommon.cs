#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using NINA.Core.Utility.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Ifttt {
    public class IftttCommon {

        public IftttCommon() {
            IFTTTWebhookKey = Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task SendIftttWebhook(string body, string eventName, CancellationToken ct) {
            string iftttWebhookHost = "https://maker.ifttt.com";

            string webhookUrl = iftttWebhookHost + "/trigger/" + eventName + "/with/key/" + IFTTTWebhookKey;
            var request = new HttpPostRequest(webhookUrl, body, "application/json");

            Logger.Debug($"Sending request to {webhookUrl}");
            await request.Request(ct);
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(IFTTTWebhookKey) || string.IsNullOrWhiteSpace(IFTTTWebhookKey)) {
                issues.Add("IFTTT Webhooks key is missing");
            }

            return issues;
        }

        private string IFTTTWebhookKey { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "IFTTTWebhookKey":
                    IFTTTWebhookKey = Security.Decrypt(Properties.Settings.Default.IFTTTWebhookKey);
                    break;
            }
        }
    }
}
