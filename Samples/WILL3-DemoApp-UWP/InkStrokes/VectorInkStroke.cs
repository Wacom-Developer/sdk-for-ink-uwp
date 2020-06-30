using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Input;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

    public class VectorInkStroke : IInkStroke
    {
        public MediaColor Color;
        public Polygon Polygon;
        public List<PolygonVertices> SimplPoly;
        public PointerDeviceType PointerDeviceType { get; set; }
        private VectorSplineInkBuilder mInkBuilder = new VectorSplineInkBuilder();

        public VectorInkStroke(Stroke stroke, Ink.Serialization.Model.VectorBrush vectorBrush, PipelineData pipelineData)
        {
            Id = stroke.Id;
            PathPointProperties ppp = stroke.Style.PathPointProperties;
            Color = MediaColor.FromArgb(
                            ppp.Alpha.HasValue ? (byte)(ppp.Alpha * 255.0f) : byte.MinValue,
                            ppp.Red.HasValue ? (byte)(ppp.Red * 255.0f) : byte.MinValue,
                            ppp.Green.HasValue ? (byte)(ppp.Green * 255.0f) : byte.MinValue,
                            ppp.Blue.HasValue ? (byte)(ppp.Blue * 255.0f) : byte.MinValue);

            Spline = stroke.Spline;
            Layout = stroke.Layout;
            VectorBrush = new Wacom.Ink.Geometry.VectorBrush(vectorBrush.BrushPolygons.ToArray());
            Polygon = PolygonUtil.ConvertPolygon(pipelineData.Merged.Addition);
            SimplPoly = pipelineData.Merged.Addition;
            SensorDataOffset = stroke.SensorDataOffset;
            SensorDataMappings = stroke.SensorDataMappings;
            SensorDataId = stroke.SensorDataId;
        }

        public VectorInkStroke(PointerDeviceType pointerDeviceType, VectorInkBuilder inkBuilder, MediaColor color, List<PolygonVertices> mergedPolygons, Ink.Geometry.VectorBrush vectorBrush, Identifier sensorDataId)
        {
            Id = Identifier.FromNewGuid();
            ZIndex = DateTime.Now.Ticks;
            Color = color;
            Polygon = ConvertPolygon(mergedPolygons);

            PointerDeviceType = pointerDeviceType;
            SensorDataId = sensorDataId;

            // Cloning is needed, otherwise the spatial data is corrupted
            Spline = inkBuilder.SplineProducer.AllData.Clone();
            Layout = inkBuilder.Layout;
            VectorBrush = vectorBrush;
            SimplPoly = mergedPolygons;
        }

        public VectorInkStroke(Spline newSpline, IInkStroke originalStroke, int firstPointIndex, int pointsCount)
        {
            Id = Identifier.FromNewGuid();
            
            // Cloning is needed, otherwise the spatial data is corrupted
            Spline = newSpline;
            Layout = originalStroke.Layout;
            VectorBrush = originalStroke.VectorBrush;
            SensorDataOffset = originalStroke.SensorDataOffset + (uint)firstPointIndex; // add the original stroke sensor data offset as a stroke can be split multiple times
            SensorDataId = ((VectorInkStroke)originalStroke).SensorDataId;

            if (originalStroke.SensorDataMappings != null)
            {
                // Calculate the sensor data mappings for the new spline
                List<uint> newSplineSensorDataMappings = new List<uint>(pointsCount);

                for (int i = 0; i < pointsCount; i++)
                {
                    newSplineSensorDataMappings.Add(originalStroke.SensorDataMappings[firstPointIndex + i]);
                }

                SensorDataMappings = newSplineSensorDataMappings;
            }

            IStrokeAttributes originConstants = originalStroke.Constants;
            Color.R = (byte)(originConstants.Red * 255);
            Color.G = (byte)(originConstants.Green * 255);
            Color.B = (byte)(originConstants.Blue * 255);
            Color.A = (byte)(originConstants.Alpha * 255);
        }

        public void UpdateSpline(Spline newSpline)
        {
            var result = mInkBuilder.AddWholePath(newSpline, Layout, VectorBrush);

            Spline = newSpline;
            SimplPoly = result.Merged.Addition;
            Polygon = PolygonUtil.ConvertPolygon(SimplPoly);
        }

        #region IInkStroke

        public long ZIndex { get; private set; }

        public Identifier Id { get; set; }

        public Spline Spline { get; set; }

        public PathPointLayout Layout { get; set; }

        public Wacom.Ink.Geometry.VectorBrush VectorBrush { get; set; }

        public IStrokeAttributes Constants { get => new Attributes(Color); }


        public uint SensorDataOffset { get; set; }

        public IReadOnlyList<uint> SensorDataMappings { get; set; }

        public Identifier SensorDataId { get; set; } = Identifier.Empty;

        #endregion

        public override bool Equals(object obj)
        {
            if ((obj is Identifier name))
                return name == Id;
            else if (!(obj is VectorInkStroke other))
                return false;
            else
                return other.Id == Id;
        }

        /// <summary>
        /// Required due to override of Equals method
        /// </summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private static Polygon ConvertPolygon(List<PolygonVertices> src)
        {
            Polygon dest = new Polygon();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }

            return dest;
        }


        public class Attributes : IStrokeAttributes
        {
            public Attributes(MediaColor color)
            {
                Red = color.R / 255f;
                Green = color.G / 255f;
                Blue = color.B / 255f;
                Alpha = color.A / 255f;
            }

            public float Size => 1.0f;

            public float Rotation => 0.0f;

            public float ScaleX => 1.0f;

            public float ScaleY => 1.0f;

            public float ScaleZ => 1.0f;

            public float OffsetX => 0.0f;

            public float OffsetY => 0.0f;

            public float OffsetZ => 0.0f;

            public float Red { get; private set; }

            public float Green { get; private set; }

            public float Blue { get; private set; }

            public float Alpha { get; private set; }
        }

    }


}
