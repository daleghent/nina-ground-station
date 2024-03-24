#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NINA.Core.Utility;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Email {

    public class EmailCommon {

        public EmailCommon() {
        }

        public static async Task SendEmail(MimeMessage message, CancellationToken ct) {
            var smtpHostName = GroundStation.GroundStationConfig.SmtpHostName;
            var smtpHostPort = GroundStation.GroundStationConfig.SmtpHostPort;
            var smtpUsername = GroundStation.GroundStationConfig.SmtpUsername;
            var smtpPassword = GroundStation.GroundStationConfig.SmtpPassword;

            var xMailerHeader = new Header("X-Mailer", $"Ground Station/{GroundStation.GetVersion()}, NINA/{CoreUtil.Version}");
            message.Headers.Add(xMailerHeader);

            var smtp = new SmtpClient();

            try {
                await smtp.ConnectAsync(smtpHostName, smtpHostPort, SecureSocketOptions.Auto, ct);

                if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword)) {
                    await smtp.AuthenticateAsync(smtpUsername, smtpPassword, ct);
                }

                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);
            } catch (SocketException ex) {
                Logger.Error($"SmtpEmail: Connection to {smtpHostName}:{smtpHostPort} failed: {ex.SocketErrorCode}: {ex.Message}");
                throw;
            } catch (AuthenticationException ex) {
                Logger.Error($"SendEmail: User {smtpUsername} failed to authenticate with {smtpHostName}:{smtpHostPort}: {ex.Message}");
                throw;
            }
        }

        public static IList<string> ValidateSettings() {
            var issues = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.SmtpHostName)) {
                issues.Add("SMTP server is not configured");
            }

            if (GroundStation.GroundStationConfig.SmtpHostPort < 1) {
                issues.Add("SMTP port is invalid");
            }

            return issues;
        }
    }
}