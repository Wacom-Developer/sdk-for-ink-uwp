using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        //public PathPointLayout Layout { get; set; }

        public ParticleList Path { get; }
        public StrokeConstants StrokeConstants { get; }
        public uint RandomSeed { get; }

        public PointerDeviceType PointerDeviceType { get; set; }

        public Identifier SensorDataId { get; set; }

        public RasterBrush RasterBrush { get; set; }

        public ParticleBrush ParticleBrush { get; set; }

        public RasterInkStroke(RasterInkBuilder inkBuilder, 
            PointerDeviceType pointerDeviceType, 
            Path points, 
            uint seed, 
            RasterBrush rasterBrush,
            ParticleBrush particleBrush,
            StrokeConstants StrokeParams, 
            Identifier sensorDataId)
        {
            Id = Identifier.FromNewGuid();

            PointerDeviceType = pointerDeviceType;

            Path = new ParticleList();
            Path.Assign(points, (uint)points.LayoutMask);

            RandomSeed = seed;
            StrokeConstants = StrokeParams;
            SensorDataId = sensorDataId;
            RasterBrush = rasterBrush;
            ParticleBrush = particleBrush;

            // Cloning is needed, otherwise the spatial data is corrupted
            Spline = inkBuilder.SplineAccumulator.Accumulated.Clone();
            //Layout = inkBuilder.Layout;
        }

        public RasterInkStroke(Stroke stroke, RasterBrush rasterBrush, ParticleList particleList, ParticleBrush particleBrush)
        {
            Id = stroke.Id;
            Path = particleList;
            RandomSeed = stroke.RandomSeed;
            RasterBrush = rasterBrush;
            ParticleBrush = particleBrush;

            PathPointProperties ppp = stroke.Style.PathPointProperties;

            StrokeConstants = new StrokeConstants
            {
                Color = MediaColor.FromArgb(
                    (byte)(ppp.Alpha * 255.0f),
                    (byte)(ppp.Red * 255.0f),
                    (byte)(ppp.Green * 255.0f),
                    (byte)(ppp.Blue * 255.0f))
            };
            SensorDataId = stroke.SensorDataId;

            Spline = stroke.Spline.ToSpline();
            //Layout = stroke.Layout;
        }

    }

}