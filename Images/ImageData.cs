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