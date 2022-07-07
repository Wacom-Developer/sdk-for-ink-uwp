using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI;

using UimPathPointProperties = Wacom.Ink.Serialization.Model.PathPointProperties;


namespace WacomInkDemoUWP
{
    public abstract class Stroke : IInkStroke, IStrokeAttributes
    {
        #region Fields

        public Color Color { get; private set; }
        public AppBrush Brush { get; private set; }
        public float ViewToModelScale { get; private set; } = 1.0f;
        public BlendMode BlendMode { get; private set; }
        public string Tag { get; set; }

        // Cache
        public List<SplineParameter> m_splineParams;
        public Path m_interpolatedSpline;
        public List<List<Vector2>> m_brushSamples;

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
            float viewToModelScale,
            BlendMode blendMode,
            uint sensorDataOffset,
            string tag = null)
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

            ViewToModelScale = viewToModelScale;
            BlendMode = blendMode;
            Tag = tag;

            SensorDataOffset = sensorDataOffset;
        }

        public Stroke(
            Identifier id,
            Spline spline,
            AppBrush brush,
            UimPathPointProperties props,
            BlendMode blendMode,
            uint sensorDataOffset)
        {
            Id = id;
            Spline = spline;
            Brush = brush;

            SetConstants(props);

            ViewToModelScale = 1.0f;
            BlendMode = blendMode;
            Tag = null;

            SensorDataOffset = sensorDataOffset;
        }

        #endregion

        #region IInkStroke

        public Identifier Id { get; private set; }

        public Spline Spline { get; private set; }

        public VectorBrush VectorBrush => Brush.VectorBrush;

        public IStrokeAttributes Constants => this;

        public uint SensorDataOffset { get; private set; }

        public IReadOnlyList<uint> SensorDataMappings => throw new NotImplementedException();

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

        private void SetConstants(UimPathPointProperties ppp)
        {
            Size = ppp.Size;
            Rotation = ppp.Rotation;
            ScaleX = ppp.ScaleX;
            ScaleY = ppp.ScaleY;
            OffsetX = ppp.OffsetX;
            OffsetY = ppp.OffsetY;

            Color = Color.FromArgb(
                (byte)(255 * ppp.Alpha),
                (byte)(255 * ppp.Red),
                (byte)(255 * ppp.Green),
                (byte)(255 * ppp.Blue));
        }

        public int GetSplineControlPointsCount()
        {
            int controlPointsCount = Spline.Path.CalculatePointsCount();

            return controlPointsCount;
        }

        public abstract void RebuildCache(bool useNewInterpolator);

        public virtual void ClearCache()
        {
            m_splineParams = null;
            m_interpolatedSpline = null;
            m_brushSamples = null;
        }

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

                /*	if (oSx != -1)
                    {
                        //int iSx = start + oSx;
                        //data[iSx] = transformScale * data[iSx];
                    }*/

                /*	if (oSy != -1)
                    {
                        // FIX
                    }*/
            }
        }

        /// <summary>
        /// Rotates the stroke around pin point (0, 0).
        /// </summary>
        /// <param name="angle">Rotation angle in radians.</param>
        public void RotateStroke(float angle)
        {
            Matrix3x2 rotMatrix = Matrix3x2.CreateRotation(angle);

            TransformPath(Spline.Path, rotMatrix, 1.0f);
            Rotation += angle;

            RebuildCache(true);
        }

        #region Debug

        public virtual void Dbg_Print()
        {
            Dbg.PrintFloatList(m_interpolatedSpline, "INTERPOLATED");
        }

        public string Dbg_GetSplineDataAsSourceCode()
        {
            Path path = Spline.Path;
            int pointsCount = GetSplineControlPointsCount();
            int stride = path.LayoutMask.GetChannelsCount();

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Path data = new Path((LayoutMask){(int)Spline.LayoutMask});");
            sb.AppendLine("data.AddRange(new float[] {");

            int index = 0;

            for (int i = 0; i < pointsCount; i++)
            {
                for (int k = 0; k < stride; k++)
                {
                    sb.Append(path[index]);
                    sb.Append("f");
                    index++;

                    bool isLastElement = (i == pointsCount - 1) && (k == stride - 1);

                    if (!isLastElement)
                    {
                        sb.Append(",");
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("});");
            sb.AppendLine($"return new Spline(data, {Spline.Ts}f, {Spline.Tf}f);");

            return sb.ToString();
        }

        #endregion
    }

    public class VectorStroke : Stroke
    {
        #region Fields

        // Cache
        public List<List<Vector2>> m_hulls;
        public List<List<Vector2>> m_nonSimplifiedContours; // outer contour + holes before simplification
        public List<List<Vector2>> m_contours;              // outer contour + holes after simplification
        public Polygon m_polygon;

        #endregion

        #region Constructors

        public VectorStroke(
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
            float viewToModelScale,
            BlendMode blendMode,
            uint sensorDataOffset = 0,
            string tag = null) :
            base(id, spline, color, brush, size, rotation, scaleX, scaleY, offsetX, offsetY, viewToModelScale, blendMode, sensorDataOffset, tag)
        {
        }

        public VectorStroke(
            Identifier id,
            Spline spline,
            AppBrush brush,
            UimPathPointProperties props,
            BlendMode blendMode,
            uint sensorDataOffset = 0) :
            base(id, spline, brush, props, blendMode, sensorDataOffset)
        {
        }

        #endregion

        #region Stroke API

        public override void RebuildCache(bool useNewInterpolator)
        {
            Interpolate(useNewInterpolator);

            BrushApplier brushApplier = new BrushApplier(VectorBrush)
            {
                Prototype = Brush.VectorBrush,
                DefaultSize = Size,
                DefaultRotation = Rotation,
                DefaultScale = new Vector3(ScaleX, ScaleY, 1.0f),
                DefaultOffset = new Vector3(OffsetX, OffsetY, 0.0f)
            };

            m_brushSamples = brushApplier.Add(true, true, m_interpolatedSpline, null).Addition;

            ConvexHullChainProducer hullProducer = new ConvexHullChainProducer();
            m_hulls = hullProducer.Add(true, true, m_brushSamples, null).Addition;

            PolygonMerger merger = new PolygonMerger();
            m_nonSimplifiedContours = merger.Add(true, true, m_hulls, null).Addition;

            bool simplifyContours = false;

            if (simplifyContours)
            {
                PolygonSimplifier simplifier = new PolygonSimplifier(0.1f);

                m_contours = simplifier.Add(true, true, m_nonSimplifiedContours, null).Addition;
            }
            else
            {
                m_contours = m_nonSimplifiedContours;
            }

            m_polygon = PolygonUtil.ConvertPolygon(m_contours);
        }

        public override void ClearCache()
        {
            base.ClearCache();

            m_hulls = null;
            m_polygon = null;
        }

        public override bool HasCache()
        {
            return m_polygon != null;
        }

        public int GetContoursCount()
        {
            return m_contours.Count;
        }

        public int CalculateContourPointsCount()
        {
            int total = 0;

            foreach (var contour in m_contours)
            {
                total += contour.Count;
            }

            return total;
        }

        #endregion

        #region Implementation

        private void Interpolate(bool useNewInterpolator)
        {
            SplineInterpolator si;

            if (useNewInterpolator)
            {
                var csi = new CurvatureBasedInterpolator
                {
                    ErrorThreshold = 0.15f * ViewToModelScale
                };
                si = csi;
            }
            else
            {
                si = new DistanceBasedInterpolator(1.0f);
            }

            si.AccumulateSplineParameters = true;
            si.Process(true, true, Spline, null);

            int sampledPointsCount = si.Addition.Count / si.Addition.LayoutMask.GetChannelsCount();

            Debug.Assert(si.SplineParameters.Count == sampledPointsCount);

            m_splineParams = si.SplineParameters;
            m_interpolatedSpline = si.Addition;
        }

        #endregion

        #region Debug

        public override void Dbg_Print()
        {
            base.Dbg_Print();

            Dbg.PrintListOfPolygons(m_brushSamples, "BRUSH SAMPLES");

            Dbg.PrintListOfPolygons(m_hulls, "HULLS");

            Dbg.PrintListOfPolygons(m_nonSimplifiedContours, "MERGED");

            Dbg.PrintListOfPolygons(m_contours, "SIMPLIFIED");
        }

        public void Dbg_PrintHullsAsSourceCode()
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder sb1 = new StringBuilder();

            sb1.AppendLine("static int[][] codes = new int[][]");
            sb1.AppendLine("{");

            int k = 0;

            foreach (var hull in m_hulls)
            {
                sb1.AppendLine($"HULL_{k},");

                sb.AppendLine($"public static int[] HULL_{k} = new int[]");
                sb.AppendLine("{");

                foreach (var point in hull)
                {
                    int sx = BitConverter.SingleToInt32Bits(point.X);
                    sb.AppendLine("\t" + sx.ToString() + ",");

                    int sy = BitConverter.SingleToInt32Bits(point.Y);
                    sb.AppendLine("\t" + sy.ToString() + ",");

                    //if (BitConverter.Int32BitsToSingle(sx) != point.X){ }
                    //if (BitConverter.Int32BitsToSingle(sy) != point.Y){ }
                }

                sb.AppendLine("};\n");

                k++;
            }

            sb1.AppendLine("};");

            Debug.WriteLine(sb.ToString());
            Debug.WriteLine(sb1.ToString());
        }

        #endregion
    }

    public class RasterStroke : Stroke
    {
        #region Constructors

        public RasterStroke(
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
            float viewToModelScale,
            BlendMode blendMode,
            uint randomSeed,
            uint sensorDataOffset = 0,
            string tag = null) :
            base(id, spline, color, brush, size, rotation, scaleX, scaleY, offsetX, offsetY, viewToModelScale, blendMode, sensorDataOffset, tag)
        {
            RandomSeed = randomSeed;
        }

        public RasterStroke(
            Identifier id,
            Spline spline,
            AppBrush brush,
            UimPathPointProperties props,
            BlendMode blendMode,
            uint randomSeed,
            uint sensorDataOffset = 0) :
            base(id, spline, brush, props, blendMode, sensorDataOffset)
        {
            RandomSeed = randomSeed;
        }

        #endregion

        #region Properties

        public uint RandomSeed { get; private set; }

        public float ParticleSpacing => Brush.Spacing;

        public ParticleList Particles { get; } = new ParticleList();

        #endregion

        #region Overrides from Stroke

        public override void RebuildCache(bool useNewInterpolator)
        {
            DistanceBasedInterpolator interpolator = new DistanceBasedInterpolator(
                spacing: Brush.Spacing,
                splitCount: 6,
                interpolateByLength: true,
                calculateTangents: true,
                keepAllData: false)
            {
                DefaultSize = Size,
                AccumulateSplineParameters = true
            };

            BrushApplier brushApplier = new BrushApplier(VectorBrush);
            brushApplier.SetDataProvider(interpolator);
            brushApplier.Prototype = Brush.VectorBrush;
            brushApplier.DefaultSize = Size;
            brushApplier.DefaultRotation = Rotation;
            brushApplier.DefaultScale = new Vector3(ScaleX, ScaleY, 1.0f);
            brushApplier.DefaultOffset = new Vector3(OffsetX, OffsetY, 0.0f);

            interpolator.Process(true, true, Spline, null);
            brushApplier.Process();

            m_splineParams = interpolator.SplineParameters;
            m_interpolatedSpline = interpolator.Addition;
            m_brushSamples = brushApplier.Addition;

            Particles.Assign(m_interpolatedSpline, (uint)m_interpolatedSpline.LayoutMask);
        }

        public override void ClearCache()
        {
            base.ClearCache();
            Particles.RemoveAll();
        }

        public override bool HasCache()
        {
            return !Particles.IsEmpty;
        }

        #endregion
    }
}