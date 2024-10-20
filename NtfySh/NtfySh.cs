#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using ntfy;
using ntfy.Requests;
using System;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.NtfySh {
    public class NtfySh {

        public NtfySh() {
            NtfyShUrl = GroundStation.GroundStationConfig.NtfyShUrl;
            NtfyShTopic = GroundStation.GroundStationConfig.NtfyShDefaultTopic;
            NtfyShIcon = GroundStation.GroundStationConfig.NtfyShDefaultIcon;

            NtfyShUser = GroundStation.GroundStationConfig.NtfyShUser;
            NtfyShPassword = GroundStation.GroundStationConfig.NtfyShPassword;
            NtfyShToken = GroundStation.GroundStationConfig.NtfyShToken;
        }

        public async Task SendNftyShMessage() {
            try {
                User user = null;
                var client = new Client(NtfyShUrl);

                if (!string.IsNullOrEmpty(NtfyShToken)) {
                    user = new User(NtfyShToken);
                } else if (!string.IsNullOrEmpty(NtfyShUser) && !string.IsNullOrEmpty(NtfyShPassword)) {
                    user = new User(NtfyShUser, NtfyShPassword);
                }

                var message = new SendingMessage();

                if (!string.IsNullOrEmpty(NtfyShTitle)) {
                    message.Title = NtfyShTitle;
                }

                if (!string.IsNullOrEmpty(NtfyShMessage)) {
                    message.Message = NtfyShMessage;
                }

                if (!string.IsNullOrEmpty(NtfyShTags)) {
                    message.Tags = NtfyShTags.Split(',', StringSplitOptions.TrimEntries);
                }

                message.Priority = NtfyShPrioirty;

                if (!string.IsNullOrEmpty(NtfyShIcon)) {
                    message.Icon = NtfyShIcon;
                }

                await client.Publish(NtfyShTopic.Trim(), message, user);
            } catch (Exception ex) {
                throw new Exception($"Failed to send ntfy message: {ex.Message}");
            }
        }

        public static PriorityLevel GsNtfyPrio2PriorityLevel(NtfyShPriorityLevels priority) {
            return priority switch {
                NtfyShPriorityLevels.Default => PriorityLevel.Default,
                NtfyShPriorityLevels.High => PriorityLevel.High,
                NtfyShPriorityLevels.Low => PriorityLevel.Low,
                NtfyShPriorityLevels.Max => PriorityLevel.Max,
                NtfyShPriorityLevels.Min => PriorityLevel.Min,
                _ => PriorityLevel.Default,
            };
        }

        public string NtfyShUrl { get; set; }
        public string NtfyShTopic { get; set; } = string.Empty;
        public string NtfyShTitle { get; set; } = string.Empty;
        public string NtfyShMessage { get; set; } = string.Empty;
        public string NtfyShTags { get; set; } = string.Empty;
        public string NtfyShIcon { get; set; } = string.Empty;
        public PriorityLevel NtfyShPrioirty { get; set; } = PriorityLevel.Default;

        private string NtfyShUser { get; set; }
        private string NtfyShPassword { get; set; }
        private string NtfyShToken { get; set; }
    }
}
