#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace DaleGhent.NINA.GroundStation.Telegram {
    public class TelegramCommon {

        public TelegramCommon() {
            TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
            TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);

            Properties.Settings.Default.PropertyChanged += SettingsChanged;
        }

        public async Task SendTelegram(string message, bool doNotNotify, CancellationToken ct) {
            var bclient = new TelegramBotClient(TelegramAccessToken);

            Logger.Debug("Pushing message");

            try {
                await bclient.SendTextMessageAsync(TelegramChatId, message, disableNotification: doNotNotify, cancellationToken: ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to Telegram: {ex.Message}");
                throw ex;
            }
        }

        public IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(TelegramAccessToken) || string.IsNullOrWhiteSpace(TelegramAccessToken)) {
                issues.Add("Telegram bot access token is missing");
            }

            if (string.IsNullOrEmpty(TelegramChatId) || string.IsNullOrWhiteSpace(TelegramChatId)) {
                issues.Add("Telegram chat ID missing");
            }

            return issues;
        }

        private string TelegramAccessToken { get; set; }
        private string TelegramChatId { get; set; }

        void SettingsChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case "TelegramAccessToken":
                    TelegramAccessToken = Security.Decrypt(Properties.Settings.Default.TelegramAccessToken);
                    break;
                case "TelegramChatId":
                    TelegramChatId = Security.Decrypt(Properties.Settings.Default.TelegramChatId);
                    break;
            }
        }
    }
}
