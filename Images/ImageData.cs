using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using System.IO;

namespace DaleGhent.NINA.GroundStation.Images {
    public class ImageData {
        public MemoryStream PngBitMap { get; set; } = new();
        public ImageMetaData ImageMetaData { get; set; } = new();
        public IImageStatistics ImageStatistics { get; set; }
        public IStarDetectionAnalysis StarDetectionAnalysis { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }
}