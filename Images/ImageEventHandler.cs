using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;

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
            ImageService.Instance.Image.Bitmap.Dispose();
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

            var memoryStream = new MemoryStream();

            try {
                var imageData = await imageDataFactory.CreateFromFile(msg.PathToImage.LocalPath, bitDepth, isBayered, rawConverter);
                var renderedImage = imageData.RenderImage();

                if (isBayered && profileService.ActiveProfile.ImageSettings.DebayerImage) {
                    renderedImage = renderedImage.Debayer(saveColorChannels: unlinkedStretch, bayerPattern: bayerPattern);
                }

                renderedImage = await renderedImage.Stretch(stretchFactor, blackClipping, unlinkedStretch);
                BitmapFrame bitmapFrame;

                if (GroundStation.GroundStationConfig.ImageServiceImageScaling < 100) {
                    var scaling = GroundStation.GroundStationConfig.ImageServiceImageScaling / 100d;

                    var transform = new ScaleTransform(scaling, scaling);
                    var scaledBitmap = new TransformedBitmap(renderedImage.Image, transform);
                    bitmapFrame = BitmapFrame.Create(scaledBitmap);
                } else {
                    bitmapFrame = BitmapFrame.Create(renderedImage.Image);
                }

                string contentType = string.Empty;
                string fileExtension = string.Empty;

                switch ((ImageFormatEnum)GroundStation.GroundStationConfig.ImageServiceFormat) {
                    case ImageFormatEnum.JPEG:
                        var jpegBitmap = new JpegBitmapEncoder();
                        jpegBitmap.Frames.Add(BitmapFrame.Create(bitmapFrame));
                        jpegBitmap.Save(memoryStream);
                        contentType = "image/jpeg";
                        fileExtension = "jpg";
                        break;

                    case ImageFormatEnum.PNG:
                        var pngBitmap = new PngBitmapEncoder();
                        pngBitmap.Frames.Add(bitmapFrame);
                        pngBitmap.Save(memoryStream);
                        contentType = "image/png";
                        fileExtension = "png";
                        break;
                }

                var ImageData = new ImageData() {
                    Bitmap = memoryStream,
                    ImageMetaData = msg.MetaData,
                    ImageMimeType = contentType,
                    ImageFileExtension = fileExtension,
                    ImageStatistics = msg.Statistics,
                    StarDetectionAnalysis = msg.StarDetectionAnalysis,
                    ImagePath = msg.PathToImage.LocalPath,
                };

                ImageService.Instance.Image = ImageData;
            } catch (Exception ex) {
                Logger.Error($"Exception: {ex.Message} {ex.StackTrace}");
            } finally {
                memoryStream.Dispose();
            }
        }
    }
}