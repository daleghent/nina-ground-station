#region "copyright"

/*
    Copyright 2023 Dale Ghent <daleg@elemental.org>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/
*/

#endregion "copyright"

using DaleGhent.NINA.GroundStation.Images;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Image.Interfaces;
using System;
using System.Linq;
using System.Threading;

namespace DaleGhent.NINA.GroundStation.Mqtt {
    public class MqttEvents {

        public MqttEvents() {
        }

        public static void Start() {
            Logger.Debug("Starting MqttEvents instance");
            ImageService.Instance.ImageUpdatedEvent += PublishImageToBroker;
        }

        public static void Stop() {
            Logger.Debug("Stopping MqttEvents instance");
            ImageService.Instance.ImageUpdatedEvent -= PublishImageToBroker;
        }

        private static async void PublishImageToBroker() {
            if (!GroundStation.GroundStationConfig.MqttImagePubliserEnabled) {
                return;
            }

            var imageData = ImageService.Instance.Image;

            if (!GroundStation.GroundStationConfig.MqttImageTypesSelected.Split(',').Contains(imageData.ImageMetaData.Image.ImageType)) {
                return;
            }

            var json = JsonConvert.SerializeObject(new MetadataObject {
                ImageMetaData = imageData.ImageMetaData,
                ImageStatistics = imageData.ImageStatistics,
                StarDetectionAnalysis = imageData.StarDetectionAnalysis,
                ImagePath = imageData.ImagePath,
            }, Formatting.None, new NoNanRealConverter());

            var topic = GroundStation.GroundStationConfig.MqttImagePublisherMetdataTopic;
            var qos = GroundStation.GroundStationConfig.MqttImagePublisherQoSLevel;
            var contentType = "application/json";

            await MqttCommon.PublishMessage(topic, json, qos, CancellationToken.None, contentType);

            if (!GroundStation.GroundStationConfig.MqttImagePubliserMetadataOnly) {
                topic = GroundStation.GroundStationConfig.MqttImagePublisherImageTopic;
                contentType = "image/jpeg";

                await MqttCommon.PublishByteMessage(topic, ImageService.Instance.Image.JpegBitMap.ToArray(), qos, contentType, CancellationToken.None);
            }
        }

        public class MetadataObject {
            public ImageMetaData ImageMetaData { get; set; }
            public IImageStatistics ImageStatistics { get; set; }
            public IStarDetectionAnalysis StarDetectionAnalysis { get; set; }
            public string ImagePath { get; set; }
        }

        public class NoNanRealConverter : JsonConverter {
            public override bool CanConvert(Type objectType) {
                var type = Nullable.GetUnderlyingType(objectType) ?? objectType;
                return new[] { typeof(float), typeof(double), typeof(decimal) }.Contains(type);
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
                var nullableBase = Nullable.GetUnderlyingType(objectType);
                var type = nullableBase ?? objectType;
                if (nullableBase != null && reader.TokenType == JsonToken.Null)
                    return null;
                if (type == typeof(double)) {
                    var value = Convert.ToDouble(reader.Value);
                    return double.IsNaN(value) ? null : value;
                } else if (type == typeof(float)) {
                    var value = Convert.ToSingle(reader.Value);
                    return float.IsNaN(value) ? null : value;
                }
                return Convert.ToDecimal(reader.Value);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
                if (value == null)
                    writer.WriteNull();
                else if (value is double d && double.IsNaN(d))
                    writer.WriteNull();
                else if (value is float f && float.IsNaN(f))
                    writer.WriteNull();
                else
                    writer.WriteValue(value);
            }
        }
    }
}