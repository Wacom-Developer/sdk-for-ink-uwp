using System;
using System.Collections.Generic;
using Windows.Devices.Input;
using Windows.Foundation;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{

    public class RasterInkStroke //: IInkStroke
    {
        public Identifier Id { get; set; }

        public Spline Spline { get; set; }
        public PathPointLayout Layout { get; set; }

        public ParticleList Path { get; }
        public StrokeConstants StrokeConstants { get; }
        public uint RandomSeed { get; }

        public PointerDeviceType PointerDeviceType { get; set; }

        public Identifier SensorDataId { get; set; }

        public RasterBrush RasterBrush { get; set; }

        public RasterInkStroke(RasterInkBuilder inkBuilder, PointerDeviceType pointerDeviceType, List<float> points, uint seed, RasterBrush rasterBrush, StrokeConstants StrokeParams, Identifier sensorDataId)
        {
            Id = Identifier.FromNewGuid();

            PointerDeviceType = pointerDeviceType;

            uint channelMask = (uint)inkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;

            Path = new ParticleList(channelMask);
            Path.Assign(points);

            RandomSeed = seed;
            StrokeConstants = StrokeParams;
            SensorDataId = sensorDataId;
            RasterBrush = rasterBrush;

            // Cloning is needed, otherwise the spatial data is corrupted
            Spline = inkBuilder.SplineProducer.AllData.Clone();
            Layout = inkBuilder.Layout;
        }

        public RasterInkStroke(Stroke stroke, RasterBrush rasterBrush, ParticleList particleList)
        {
            Id = stroke.Id;
            Path = particleList;
            RandomSeed = stroke.Style.RandomSeed;
            RasterBrush = rasterBrush;

            PathPointProperties ppp = stroke.Style.PathPointProperties;

            StrokeConstants = new StrokeConstants
            {
                Color = MediaColor.FromArgb(
                            ppp.Alpha.HasValue ? (byte)(ppp.Alpha * 255.0f) : byte.MinValue,
                            ppp.Red.HasValue ? (byte)(ppp.Red * 255.0f) : byte.MinValue,
                            ppp.Green.HasValue ? (byte)(ppp.Green * 255.0f) : byte.MinValue,
                            ppp.Blue.HasValue ? (byte)(ppp.Blue * 255.0f) : byte.MinValue)
            };
            SensorDataId = stroke.SensorDataId;

            Spline = stroke.Spline;
            Layout = stroke.Layout;
        }

    }

}