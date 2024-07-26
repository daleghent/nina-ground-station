#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using NINA.Core.Locale;
using NINA.Core.Utility;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DaleGhent.NINA.GroundStation.Slack {
    public class SlackEvents {

        public SlackEvents() {
        }

        public static void Start() {
            Logger.Debug("Starting SlackEvents instance");
            ImageService.Instance.ImageUpdatedEvent += PostImageToSlack;
        }

        public static void Stop() {
            Logger.Debug("Stopping SlackEvents instance");
            ImageService.Instance.ImageUpdatedEvent -= PostImageToSlack;
        }

        private static void PostImageToSlack() {
            if (!GroundStation.GroundStationConfig.SlackImageEventEnabled || string.IsNullOrEmpty(GroundStation.GroundStationConfig.SlackOAuthToken)) {
                return;
            }

            var slack = new SlackClient();

            if (SlackClient.CommonValidations().Count > 1) {
                return;
            }

            var imageData = ImageService.Instance.Image;

            if (!GroundStation.GroundStationConfig.SlackImageTypesSelected.Split(',').Contains(imageData.ImageMetaData.Image.ImageType)) {
                return;
            }

            Logger.Trace("Posting image to Slack");

            var bodyText = new StringBuilder();

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Sequence.Title)) {
                bodyText.AppendLine($"*{Loc.Instance["LblSequence"]}:* {imageData.ImageMetaData.Sequence.Title}");
            }

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Target.Name)) {
                bodyText.AppendLine($"*{Loc.Instance["LblTarget"]}:* {imageData.ImageMetaData.Target.Name}");
                bodyText.AppendLine();
            }


            bodyText.AppendLine($"*{Loc.Instance["LblCamera"]}:* {imageData.ImageMetaData.Camera.Name}");

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.Telescope.Name)) {
                bodyText.AppendLine($"*{Loc.Instance["LblTelescope"]}:* {imageData.ImageMetaData.Telescope.Name} @ {imageData.ImageMetaData.Telescope.FocalLength}mm");
            }

            if (!string.IsNullOrEmpty(imageData.ImageMetaData.FilterWheel.Filter)) {
                bodyText.AppendLine($"*{Loc.Instance["LblFilter"]}:* {imageData.ImageMetaData.FilterWheel.Filter}");
            }

            bodyText.AppendLine();

            bodyText.AppendLine($"*{Loc.Instance["LblStatistics"]}:*");
            bodyText.AppendLine($"*{Loc.Instance["LblExposureTime"]}:* {imageData.ImageMetaData.Image.ExposureTime:F2}s, *{Loc.Instance["LblGain"]}:* {imageData.ImageMetaData.Camera.Gain}, *{Loc.Instance["LblOffset"]}:* {imageData.ImageMetaData.Camera.Offset}");
            bodyText.AppendLine($"*{Loc.Instance["LblMean"]}:* {imageData.ImageStatistics.Mean:F2}, *{Loc.Instance["LblStDev"]}:* {imageData.ImageStatistics.StDev:F2}");
            bodyText.AppendLine($"*{Loc.Instance["LblMedian"]}:* {imageData.ImageStatistics.Median:F2}, *{Loc.Instance["LblMAD"]}:* {imageData.ImageStatistics.MedianAbsoluteDeviation:F2}");
            bodyText.AppendLine($"*{Loc.Instance["LblMin"]}:* {imageData.ImageStatistics.Min} (x{imageData.ImageStatistics.MinOccurrences}), *{Loc.Instance["LblMax"]}:* {imageData.ImageStatistics.Max} (x{imageData.ImageStatistics.MaxOccurrences})");
            bodyText.AppendLine($"*{Loc.Instance["LblStarCount"]}:* {imageData.StarDetectionAnalysis.DetectedStars}, *{Loc.Instance["LblHFR"]}:* {imageData.StarDetectionAnalysis.HFR:F2}");
            bodyText.AppendLine($"*{Loc.Instance["LblBitDepth"]}:* {imageData.ImageStatistics.BitDepth}, *{Loc.Instance["LblHFRStDev"]}:* {imageData.StarDetectionAnalysis.HFRStDev:F2}");

            var slackImage = new SlackImage() {
                Title = $"{Path.GetFileName(imageData.ImagePath)}",
                BodyText = bodyText.ToString(),
                Channel = GroundStation.GroundStationConfig.SlackImageEventChannel,
                FileContent = imageData.Bitmap,
                FileType = imageData.ImageFileExtension,
            };

            var imageFileName = Path.GetFileName(imageData.ImagePath);
            slackImage.FileName = Path.ChangeExtension(imageFileName, imageData.ImageFileExtension);

            try {
                slack.PostImage(slackImage).Wait();
            } catch (Exception ex) {
                Logger.Error($"Error posting image to Slack: {ex.Message}");
                return;
            }
        }
    }
}