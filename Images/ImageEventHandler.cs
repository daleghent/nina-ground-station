using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace DaleGhent.NINA.GroundStation.Images {
    public class ImageEventHandler(IProfileService profileService, IImageSaveMediator imageSaveMediator, IImageDataFactory imageDataFactory) {
        private readonly IProfileService profileService = profileService;
        private readonly IImageSaveMediator imageSaveMediator = imageSaveMediator;
        private readonly IImageDataFactory imageDataFactory = imageDataFactory;

        public void Start() {
            Stop();
            imageSaveMediator.ImageSaved += ImageSaveMeditator_ImageSaved;
        }

        public void Stop() {
            imageSaveMediator.ImageSaved -= ImageSaveMeditator_ImageSaved;
            ImageService.Instance.Image.JpegBitMap.Dispose();
        }

        private async void ImageSaveMeditator_ImageSaved(object sender, ImageSavedEventArgs msg) {
            var isBayered = false;
            var bitDepth = (int)profileService.ActiveProfile.CameraSettings.BitDepth;

            var stretchFactor = profileService.ActiveProfile.ImageSettings.AutoStretchFactor;
            var blackClipping = profileService.ActiveProfile.ImageSettings.BlackClipping;
            var unlinkedStretch = profileService.ActiveProfile.ImageSettings.UnlinkedStretch;

            if (msg.MetaData.Camera.SensorType != SensorType.Monochrome) {
                isBayered = true;
            }

            try {
                var imageData = await imageDataFactory.CreateFromFile(msg.PathToImage.LocalPath, bitDepth, isBayered, RawConverterEnum.FREEIMAGE);
                var renderedImage = imageData.RenderImage();
                renderedImage = await renderedImage.Stretch(stretchFactor, blackClipping, unlinkedStretch);

                var metaData = new BitmapMetadata("jpg") {
                    ApplicationName = $"N.I.N.A. {CoreUtil.Version} / Ground Station {GroundStation.GetVersion()}",
                    Title = Path.GetFileName(msg.PathToImage.LocalPath),
                    Subject = msg.MetaData.Target?.Name,
                    DateTaken = msg.MetaData.Image.ExposureStart.ToString("o"),
                    CameraModel = msg.MetaData.Camera.Name,
                    Comment = msg.MetaData.Telescope.Name,
                };

                var jpegBitmap = new JpegBitmapEncoder {
                    QualityLevel = 90,
                };

                jpegBitmap.Frames.Add(BitmapFrame.Create(renderedImage.Image, null, metaData, null));

                using var memoryStream = new MemoryStream();
                jpegBitmap.Save(memoryStream);

                var ImageData = new ImageData() {
                    JpegBitMap = memoryStream,
                    ImageMetaData = msg.MetaData,
                    ImageStatistics = msg.Statistics,
                    StarDetectionAnalysis = msg.StarDetectionAnalysis,
                    ImagePath = msg.PathToImage.LocalPath,
                };

                ImageService.Instance.Image = ImageData;
            } catch (Exception ex) {
                Logger.Error($"Exception: {ex.Message} {ex.StackTrace}");
            }
        }
    }
}