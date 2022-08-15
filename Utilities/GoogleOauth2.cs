#region "copyright"

/*
    Copyright Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using NINA.Plugin;
using System;
using System.IO;
using System.Threading;

namespace DaleGhent.NINA.GroundStation.Utilities {

    public static class GoogleOauth2 {
        internal static readonly string KeyFileLocation = Path.Combine(Constants.UserExtensionsFolder, "Ground Station", ".gs_creds");
        internal static readonly string GoogleApiClientId = "";
        internal static readonly string GoogleApiClientSecret = "";

        private static readonly string[] gmailScopes = new[] { GmailService.Scope.GmailSend };

        public static GmailService GetGmailService(string username) {
            try {
                if (string.IsNullOrEmpty(username)) {
                    throw new ArgumentNullException("Username not specified");
                }

                var cred = GetUserCredential(username);
                return GetService(cred);
            } catch (Exception ex) {
                throw new Exception("Get Gmail service failed.", ex);
            }
        }

        private static UserCredential GetUserCredential(string username) {
            try {
                if (string.IsNullOrEmpty(username)) {
                    throw new ArgumentNullException("Username not specified");
                }

                if (!Directory.Exists(KeyFileLocation)) {
                    throw new Exception($"Credentials file does not exist: {KeyFileLocation}");
                }

                var clientSecrets = new ClientSecrets {
                    ClientId = GoogleApiClientId,
                    ClientSecret = GoogleApiClientSecret
                };

                var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    clientSecrets,
                    gmailScopes,
                    username,
                    CancellationToken.None,
                    new FileDataStore(KeyFileLocation, true)).Result;

                credential.GetAccessTokenForRequestAsync();
                return credential;
            } catch (Exception ex) {
                throw new Exception("Get user credentials failed.", ex);
            }
        }

        private static GmailService GetService(UserCredential credential) {
            try {
                if (credential == null) {
                    throw new ArgumentNullException("credential");
                }

                return new GmailService(new BaseClientService.Initializer() {
                    HttpClientInitializer = credential,
                    ApplicationName = "Ground Station"
                });
            } catch (Exception ex) {
                throw new Exception("Get Gmail service failed.", ex);
            }
        }
    }
}