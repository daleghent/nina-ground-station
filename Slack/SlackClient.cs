#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Utility;
using SlackNet;
using SlackNet.Blocks;
using SlackNet.WebApi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DaleGhent.NINA.GroundStation.Slack {

    public class SlackClient {
        private readonly ISlackApiClient slack;

        public SlackClient() {
            OAuthToken = string.IsNullOrEmpty(GroundStation.GroundStationConfig.SlackOAuthToken)
                ? throw new Exception("Slack OAuth token is not set")
                : GroundStation.GroundStationConfig.SlackOAuthToken;

            slack = new SlackServiceBuilder()
                        .UseApiToken(OAuthToken)
                        .GetApiClient();
        }

        public async Task PostMessage(Channel channel, string message) {
            try {
                var msg = new Message {
                    Text = message,
                    Channel = channel.Name,
                };

                var response = await slack.Chat.PostMessage(msg);
            } catch (Exception ex) {
                throw new Exception($"Failed to send Slack message: {ex.Message}");
            }
        }

        public async Task PostImage(SlackImage slackImage) {
            ExternalFileReference fileRef;

            try {
                Logger.Debug($"Uploading file: {slackImage.FileName}, {slackImage.FileContent.Length} bytes");

                slackImage.FileContent.Seek(0, SeekOrigin.Begin);

                var fileUpload = new FileUpload(slackImage.FileName, slackImage.FileContent) {
                    AltText = slackImage.Title,
                    Title = slackImage.Title,
                };

                fileRef = await slack.Files.Upload(fileUpload);

                Logger.Debug($"Uploaded file: {slackImage.FileName}, ID: {fileRef.Id}, Title: {fileRef.Title}");
            } catch (Exception ex) {
                throw new Exception($"Failed to upload Slack image: {ex}");
            }

            // Slacks apparently needs a moment to process an uploaded file before it can be referenced in a message block
            await Task.Delay(TimeSpan.FromSeconds(2));

            try {
                var msg = new Message {
                    Channel = slackImage.Channel.Id,
                    Blocks = [
                        new HeaderBlock {
                            Text = new PlainText(slackImage.Title),
                        },
                        new SectionBlock {
                            Text = new Markdown(slackImage.BodyText),
                        },
                        new ImageBlock {
                            AltText = slackImage.Title,
                            SlackFile = new ImageFileReference {
                                Id = fileRef.Id,
                            },
                        }
                    ]
                };

                var response = await slack.Chat.PostMessage(msg);
            } catch (Exception ex) {
                throw new Exception($"Failed to send Slack message with image attachment: {ex}");
            }
        }

        public async Task<List<Channel>> GetChannelList() {
            try {
                var list = await slack.Conversations.List(excludeArchived: true, types: [ConversationType.PublicChannel, ConversationType.PrivateChannel]);

                var channels = new List<Channel>();
                foreach (var channel in list.Channels) {
                    if (channel.IsMember && !channel.IsReadOnly) {
                        channels.Add(new Channel {
                            Id = channel.Id,
                            Name = channel.Name,
                            CreateDate = channel.CreatedDate,
                            IsPrivate = channel.IsPrivate,
                            NumMembers = channel.NumMembers,
                        });
                        Logger.Trace($"Adding channel: {channel.Name} ({channel.Id})");
                    }
                }

                return channels;
            } catch (Exception ex) {
                throw new Exception($"Failed to get Slack channel list: {ex.Message}");
            }
        }

        public async Task<BotInfo> GetBotInfo() {
            var auth = await slack.Auth.Test();
            var userInfo = await slack.Users.Info(auth.UserId);

            return new BotInfo {
                WorkspaceName = auth.Team,
                BotName = userInfo.Name,
                BotDisplayName = userInfo.RealName,
            };
        }

        public static IList<string> CommonValidations() {
            var i = new List<string>();

            if (string.IsNullOrEmpty(GroundStation.GroundStationConfig.SlackOAuthToken)) {
                i.Add("Slack OAuth token is missing");
            }

            return i;
        }

        public string OAuthToken { get; private set; }

        [JsonObject(MemberSerialization.OptIn)]
        public class BotInfo {

            [JsonProperty]
            public string WorkspaceName { get; set; }

            [JsonProperty]
            public string BotName { get; set; }

            [JsonProperty]
            public string BotDisplayName { get; set; }
        }
    }
}