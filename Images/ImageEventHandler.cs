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
            ImageService.Instance.Image.PngBitMap.Dispose();
        }

        private async void ImageSaveMeditator_ImageSaved(object sender, ImageSavedEventArgs msg) {
            var isBayered = false;
            var bitDepth = (int)profileService.ActiveProfile.CameraSettings.BitDepth;
            var rawConverter = profileService.ActiveProfile.CameraSettings.RawConverter;

            var stretchFactor = profileService.ActiveProfile.ImageSettings.AutoStretchFactor;
            var blackClipping = profileService.ActiveProfile.ImageSettings.BlackClipping;
            var unlinkedStretch = profileService.ActiveProfile.ImageSettings.UnlinkedStretch;
            var bayerPattern = msg.MetaData.Camera.SensorType;

            if (bayerPattern > SensorType.Monochrome) {
                isBayered = true;
            }

            try {
                var imageData = await imageDataFactory.CreateFromFile(msg.PathToImage.LocalPath, bitDepth, isBayered, rawConverter);
                var renderedImage = imageData.RenderImage();

                if (isBayered && profileService.ActiveProfile.ImageSettings.DebayerImage) {
                    renderedImage = renderedImage.Debayer(saveColorChannels: unlinkedStretch, bayerPattern: bayerPattern);
                }

                renderedImage = await renderedImage.Stretch(stretchFactor, blackClipping, unlinkedStretch);
                var bitmapFrame = BitmapFrame.Create(renderedImage.Image);

                var pngBitmap = new PngBitmapEncoder();
                pngBitmap.Frames.Add(bitmapFrame);

                using var memoryStream = new MemoryStream();
                pngBitmap.Save(memoryStream);

                var ImageData = new ImageData() {
                    PngBitMap = memoryStream,
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