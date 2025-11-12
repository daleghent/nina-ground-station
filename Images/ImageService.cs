#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Core.Utility;
using System;
using System.Collections.Generic;

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

        private Dictionary<ImageTypeTarget, int> imageTypeCounter = [];

        public Dictionary<ImageTypeTarget, int> ImageTypeCounter {
            get {
                lock (imageLock) {
                    return imageTypeCounter;
                }
            }
            private set => imageTypeCounter = value;
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

                    var imageTypeTarget = new ImageTypeTarget(image.ImageMetaData.Image.ImageType.ToString(), image.ImageMetaData.Target.Name.ToString());
                    ImageTypeCounter[imageTypeTarget] = !ImageTypeCounter.TryGetValue(imageTypeTarget, out int count) ? 1 : ImageTypeCounter[imageTypeTarget] + 1;

                    Logger.Debug($"ImageService: Image set. {image.ImageFormat} size = {image.Bitmap.Length} bytes, Camera = {image.ImageMetaData.Camera.Name}");
                }

                ImageUpdatedEvent?.Invoke();
            }
        }

        public sealed class ImageTypeTarget : IEquatable<ImageTypeTarget> {
            public string ImageType { get; }
            public string Target { get; }

            public ImageTypeTarget(string imageType, string target) {
                ImageType = imageType ?? throw new ArgumentNullException(nameof(imageType));
                Target = target ?? throw new ArgumentNullException(nameof(target));
            }

            public bool Equals(ImageTypeTarget? other) =>
                other is not null &&
                StringComparer.Ordinal.Equals(ImageType, other.ImageType) &&
                StringComparer.Ordinal.Equals(Target, other.Target);

            public override bool Equals(object? obj) => Equals(obj as ImageTypeTarget);

            public override int GetHashCode() =>
                HashCode.Combine(
                    StringComparer.Ordinal.GetHashCode(ImageType),
                    StringComparer.Ordinal.GetHashCode(Target));

            public override string ToString() => $"[Target: {Target}, Type: {ImageType}]";

            public static bool operator ==(ImageTypeTarget? left, ImageTypeTarget? right) =>
                EqualityComparer<ImageTypeTarget>.Default.Equals(left, right);
            public static bool operator !=(ImageTypeTarget? left, ImageTypeTarget? right) => !(left == right);
        }
    }
}
