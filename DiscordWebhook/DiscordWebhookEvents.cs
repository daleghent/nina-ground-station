#region "copyright"

/*
    Copyright 2023 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using Discord;
using NINA.Core.Locale;
using NINA.Core.Utility;
using System.IO;
using System.Linq;

namespace DaleGhent.NINA.GroundStation.DiscordWebhook {
    public class DiscordWebhookEvents {

        public DiscordWebhookEvents() {
        }

        public static void Start() {
            Logger.Debug("Starting DiscordWebhookEvents instance");
            ImageService.Instance.ImageUpdatedEvent += PostImageToDiscord;
        }

        public static void Stop() {
            Logger.Debug("Stopping DiscordWebhookEvents instance");
            ImageService.Instance.ImageUpdatedEvent -= PostImageToDiscord;
        }

        private static void PostImageToDiscord() {
            if (!GroundStation.GroundStationConfig.DiscordImageEventEnabled) {
                return;
            }

            var imageData = ImageService.Instance.Image;

            if (!GroundStation.GroundStationConfig.DiscordImageTypesSelected.Split(',').Contains(imageData.ImageMetaData.Image.ImageType)) {
                return;
            }

            Logger.Trace("Posting image to Discord webhook");

            var edgeColor = GroundStation.GroundStationConfig.DiscordImageEdgeColor;

            var embed = new EmbedBuilder {
                Title = $"{Path.GetFileName(imageData.ImagePath)}",
                Color = new Color(edgeColor.R, edgeColor.G, edgeColor.B),
                Author = new EmbedAuthorBuilder {
                    Name = GroundStation.GroundStationConfig.DiscordImagePostTitle,
                },
                Footer = new EmbedFooterBuilder {
                    Text = imageData.ImagePath,
                },
                Timestamp = imageData.ImageMetaData.Image.ExposureStart.AddSeconds(imageData.ImageMetaData.Image.ExposureTime),
            };

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Sequence.Title)) {
                embed.Description = $"{Loc.Instance["LblSequence"]} {imageData.ImageMetaData.Sequence.Title}";
            }

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Target.Name)) {
                embed.AddField(Loc.Instance["LblTarget"], imageData.ImageMetaData.Target.Name);
            }

            embed.AddField(Loc.Instance["LblCamera"], imageData.ImageMetaData.Camera.Name);

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Telescope.Name)) {
                embed.AddField(Loc.Instance["LblTelescope"], $"{imageData.ImageMetaData.Telescope.Name} @ {imageData.ImageMetaData.Telescope.FocalLength}mm");
            }

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.FilterWheel.Filter)) {
                embed.AddField(Loc.Instance["LblFilter"], imageData.ImageMetaData.FilterWheel.Filter);
            }

            embed.AddField(Loc.Instance["LblStatistics"],
                        $"**{Loc.Instance["LblExposureTime"]}:** {imageData.ImageMetaData.Image.ExposureTime:F2}s, **{Loc.Instance["LblGain"]}:** {imageData.ImageMetaData.Camera.Gain}, **{Loc.Instance["LblOffset"]}:** {imageData.ImageMetaData.Camera.Offset}\n" +
                        $"**{Loc.Instance["LblMean"]}:** {imageData.ImageStatistics.Mean:F2}, **{Loc.Instance["LblStDev"]}:** {imageData.ImageStatistics.StDev:F2}\n" +
                        $"**{Loc.Instance["LblMedian"]}:** {imageData.ImageStatistics.Median:F2}, **{Loc.Instance["LblMAD"]}:** {imageData.ImageStatistics.MedianAbsoluteDeviation:F2}\n" +
                        $"**{Loc.Instance["LblMin"]}:** {imageData.ImageStatistics.Min} (x{imageData.ImageStatistics.MinOccurrences}), **{Loc.Instance["LblMax"]}:** {imageData.ImageStatistics.Max} (x{imageData.ImageStatistics.MaxOccurrences})\n" +
                        $"**{Loc.Instance["LblStarCount"]}:** {imageData.StarDetectionAnalysis.DetectedStars}, **{Loc.Instance["LblHFR"]}:** {imageData.StarDetectionAnalysis.HFR:F2}\n" +
                        $"**{Loc.Instance["LblBitDepth"]}:** {imageData.ImageStatistics.BitDepth}, **{Loc.Instance["LblHFRStDev"]}:** {imageData.StarDetectionAnalysis.HFRStDev:F2}");

            var imageFileName = Path.GetFileName(imageData.ImagePath);
            imageFileName = Path.ChangeExtension(imageFileName, imageData.ImageFileExtension);
            embed.WithImageUrl($"attachment://{imageFileName}");

            var discordWebhookCommon = new DiscordWebhookCommon();
            discordWebhookCommon.SendDiscordImage(imageData, imageFileName, embed).Wait();
        }
    }
}