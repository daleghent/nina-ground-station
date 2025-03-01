#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using Discord;
using NINA.Core.Locale;
using NINA.Core.Utility;
using NINA.Core.Utility.Converters;
using System.Collections.Generic;
using System.Globalization;
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

            var discordWebhookCommon = new DiscordWebhookCommon();

            if (DiscordWebhookCommon.CommonValidation().Count > 1) {
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

            var nanToDoubleDash = new NaNToDoubleDashConverter();
            var negativeOneToDoubleDashConverter = new IntNegativeOneToDoubleDashConverter();

            var statsFields = new List<EmbedFieldBuilder>() {
                new() {
                    Name = Loc.Instance["LblStatistics"],
                    Value = $"{imageData.ImageMetaData.Image.ExposureTime}s, {imageData.ImageMetaData.Image.Binning}, {imageData.ImageMetaData.Image.ImageType}",
                }
            };

            statsFields.Add(new() {
                Name = Loc.Instance["LblGain"],
                Value = $"{imageData.ImageMetaData.Camera.Gain}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblOffset"],
                Value = $"{imageData.ImageMetaData.Camera.Offset}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblMean"],
                Value = $"{imageData.ImageStatistics.Mean:F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblStDev"],
                Value = $"{imageData.ImageStatistics.StDev:F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblMedian"],
                Value = $"{imageData.ImageStatistics.Median:F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblMAD"],
                Value = $"{imageData.ImageStatistics.MedianAbsoluteDeviation:F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblMin"],
                Value = $"{imageData.ImageStatistics.Min} (x{imageData.ImageStatistics.MinOccurrences})",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblMax"],
                Value = $"{imageData.ImageStatistics.Max} (x{imageData.ImageStatistics.MaxOccurrences})",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblBitDepth"],
                Value = $"{imageData.ImageStatistics.BitDepth}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblHFR"],
                Value = $"{nanToDoubleDash.Convert(imageData.StarDetectionAnalysis.HFR, typeof(string), null, CultureInfo.CurrentCulture):F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblHFRStDev"],
                Value = $"{nanToDoubleDash.Convert(imageData.StarDetectionAnalysis.HFRStDev, typeof(string), null, CultureInfo.CurrentCulture):F2}",
                IsInline = true,
            });

            statsFields.Add(new() {
                Name = Loc.Instance["LblStarCount"],
                Value = $"{negativeOneToDoubleDashConverter.Convert(imageData.StarDetectionAnalysis.DetectedStars, typeof(string), null, null)}",
                IsInline = true,
            });

            embed.WithFields(statsFields);

            var imageFileName = Path.GetFileName(imageData.ImagePath);
            imageFileName = Path.ChangeExtension(imageFileName, imageData.ImageFileExtension);
            embed.WithImageUrl($"attachment://{imageFileName}");

            var embeds = new List<Embed>() { embed.Build() };
            discordWebhookCommon.SendDiscordImage(imageData, imageFileName, embeds).Wait();
        }
    }
}