using NINA.Core.Utility;

namespace DaleGhent.NINA.GroundStation.Images {
    public delegate void ImageUpdatedEvent();

    public class ImageService {
        private static ImageService instance;
        private static readonly object imageLock = new();

        private ImageService() {
            image = new ImageData();
        }

        public event ImageUpdatedEvent ImageUpdatedEvent;

        public static ImageService Instance {
            get {
                instance ??= new ImageService();
                return instance;
            }
        }

        private ImageData image;

        public ImageData Image {
            get {
                lock (imageLock) {
                    return image;
                }
            }

            set {
                lock (imageLock) {
                    image = value;
                    ImageUpdatedEvent?.Invoke();
                    Logger.Debug($"ImageService: Image set. {image.ImageFormat} size = {image.Bitmap.Length} bytes, Camera = {image.ImageMetaData.Camera.Name}");

                }
            }
        }
    }
}
