using System;
using System.Collections.Generic;
using System.Numerics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI;

using UimPathPointProperties = Wacom.Ink.Serialization.Model.PathPointProperties;


namespace WacomInkDemoUWP
{
    abstract class Stroke : IInkStroke, IStrokeAttributes
    {
        #region Fields

        public Color Color { get; private set; }
        public AppBrush Brush { get; private set; }
        public BlendMode BlendMode { get; private set; }

        #endregion

        #region Constructors

        public Stroke(
            Identifier id,
            Spline spline,
            Color color,
            AppBrush brush,
            float size,
            float rotation,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY,
            BlendMode blendMode)
        {
            Id = id;
            Spline = spline;
            Color = color;
            Brush = brush;

            Size = size;
            Rotation = rotation;
            ScaleX = scaleX;
            ScaleY = scaleY;
            OffsetX = offsetX;
            OffsetY = offsetY;

            BlendMode = blendMode;
        }

        public Stroke(
            Identifier id,
            Spline spline,
            AppBrush brush,
            UimPathPointProperties props,
            BlendMode blendMode)
        {
            Id = id;
            Spline = spline;
            Brush = brush;

            SetConstants(props);

            BlendMode = blendMode;
        }

        #endregion

        #region IInkStroke

        public Identifier Id { get; private set; }

        public Spline Spline { get; private set; }

        public VectorBrush VectorBrush => Brush.VectorBrush;

        public IStrokeAttributes Constants => this;

        public IReadOnlyList<uint> SensorDataMappings => throw new NotImplementedException();

		public uint SensorDataOffset => throw new NotImplementedException();

		#endregion

		#region IStrokeAttributes

		public float Size { get; private set; }

        public float Rotation { get; private set; }

        public float ScaleX { get; private set; }

        public float ScaleY { get; private set; }

        public float ScaleZ => 1.0f;

        public float OffsetX { get; private set; }

        public float OffsetY { get; private set; }

        public float OffsetZ => 0.0f;

        public float Red => Color.R / (float)byte.MaxValue;

        public float Green => Color.G / (float)byte.MaxValue;

        public float Blue => Color.B / (float)byte.MaxValue;

        public float Alpha => Color.A / (float)byte.MaxValue;

		#endregion

		private void SetConstants(UimPathPointProperties props)
        {
            Size = props.Size;
            Rotation = props.Rotation;
            ScaleX = props.ScaleX;
            ScaleY = props.ScaleY;
            OffsetX = props.OffsetX;
            OffsetY = props.OffsetY;

            Color = Color.FromArgb(
                (byte)(255 * props.Alpha),
                (byte)(255 * props.Red),
                (byte)(255 * props.Green),
                (byte)(255 * props.Blue));
        }

        public int GetSplineControlPointsCount()
        {
            int controlPointsCount = Spline.Path.CalculatePointsCount();

            return controlPointsCount;
        }

        public abstract void RebuildCache();

        public abstract void ClearCache();

        public abstract bool HasCache();

        public static void TransformPath(Path path, Matrix3x2 matrix, float transformScale)
        {
            LayoutMask layout = path.LayoutMask;
            int stride = layout.GetChannelsCount();
            int controlPointsCount = path.Count / stride;

            int oX = layout.GetChannelIndex(PathPoint.Property.X);
            int oY = layout.GetChannelIndex(PathPoint.Property.Y);
            int oS = layout.GetChannelIndex(PathPoint.Property.Size);
            //int oSx = layout.GetChannelIndex(PathPoint.Property.ScaleX);
            //int oSy = layout.GetChannelIndex(PathPoint.Property.ScaleY);
            int oOx = layout.GetChannelIndex(PathPoint.Property.OffsetX);
            int oOy = layout.GetChannelIndex(PathPoint.Property.OffsetY);

            for (int i = 0; i < controlPointsCount; i++)
            {
                int start = i * stride;

                {
                    int iX = start + oX;
                    int iY = start + oY;

                    Vector2 vPos = new Vector2(path[iX], path[iY]);
                    Vector2 tPos = Vector2.Transform(vPos, matrix);
                    path[iX] = tPos.X;
                    path[iY] = tPos.Y;
                }

                if (oS != -1)
                {
                    int iS = start + oS;
                    path[iS] = transformScale * path[iS];
                }

                if (oOx != -1)
                {
                    int iOx = start + oOx;
                    path[iOx] = transformScale * path[iOx];
                }

                if (oOy != -1)
                {
                    int iOy = start + oOy;
                    path[iOy] = transformScale * path[iOy];
                }
            }
        }
    }
}