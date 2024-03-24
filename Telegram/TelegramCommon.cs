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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace DaleGhent.NINA.GroundStation.Telegram {

    public class TelegramCommon {

        public TelegramCommon() {
        }

        public static async Task SendTelegram(string message, bool doNotNotify, CancellationToken ct) {
            var bclient = new TelegramBotClient(GroundStation.GroundStationConfig.TelegramAccessToken);

            Logger.Debug("Pushing message");

            try {
                await bclient.SendTextMessageAsync(GroundStation.GroundStationConfig.TelegramChatId, message, disableNotification: doNotNotify, cancellationToken: ct);
            } catch (Exception ex) {
                Logger.Error($"Error sending to Telegram: {ex.Message}");
                throw;
            }
        }

        public static IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.TelegramAccessToken)) {
                issues.Add("Telegram bot access token is missing");
            }

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.TelegramChatId)) {
                issues.Add("Telegram chat ID missing");
            }

            return issues;
        }
    }
}