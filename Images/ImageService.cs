#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

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
