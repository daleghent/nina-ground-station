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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.IftttCommon {
    public class IftttCommon {

        public static async Task SendIftttTrigger(string body, string eventName, string key, CancellationToken ct) {
            string iftttWebhookHost = "https://maker.ifttt.com";

            string webhookUrl = iftttWebhookHost + "/trigger/" + eventName + "/with/key/" + key;
            var request = new HttpPostRequest(webhookUrl, body, "application/json");

            Logger.Debug($"IFTTT: Sending request to {webhookUrl}");
            _ = await request.Request(ct);
        }
    }
}
