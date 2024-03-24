#region "copyright"

/*
    Copyright (c) 2024 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using System.IO;

namespace DaleGhent.NINA.GroundStation.Images {
    public class ImageData {
        public MemoryStream Bitmap { get; set; } = new();
        public ImageFormatEnum ImageFormat { get; set; }
        public string ImageMimeType { get; set; }
        public string ImageFileExtension { get; set; }
        public ImageMetaData ImageMetaData { get; set; } = new();
        public IImageStatistics ImageStatistics { get; set; }
        public IStarDetectionAnalysis StarDetectionAnalysis { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }
}