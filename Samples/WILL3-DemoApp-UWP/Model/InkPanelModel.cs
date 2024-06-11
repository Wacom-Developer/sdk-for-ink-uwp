using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Wacom.Ink;
using Wacom.Ink.Manipulations;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;
using Windows.Foundation;

using GeometryVectorBrush = Wacom.Ink.Geometry.VectorBrush;
using UimBlendMode = Wacom.Ink.Serialization.Model.BlendMode;
using UimBrush = Wacom.Ink.Serialization.Model.Brush;
using UimRasterBrush = Wacom.Ink.Serialization.Model.RasterBrush;
using UimStroke = Wacom.Ink.Serialization.Model.Stroke;
using UimStyle = Wacom.Ink.Serialization.Model.Style;
using UimVectorBrush = Wacom.Ink.Serialization.Model.VectorBrush;

namespace WacomInkDemoUWP
{
    class InkPanelModel
    {
        #region Fields

        private readonly Dictionary<string, AppBrush> m_brushes = new Dictionary<string, AppBrush>();
        private readonly SpatialModel m_spatialModel = new SpatialModel();

        #endregion

        #region Properties

        public ObservableCollection<Stroke> Strokes { get; } = new ObservableCollection<Stroke>();

        #endregion

        #region Constructors

        #endregion

        #region Interface

        public void Clear()
        {
            Strokes.Clear();
            
            m_spatialModel.Clear();
            m_brushes.Clear();
        }

        public void Dispose()
        {
            Strokes.Clear();
        }

        public Stroke NodeToStroke(InkModel inkModel, InkNode node)
        {
            if (!(node is StrokeNode strokeNode))
                return null;

            UimStroke uimStroke = strokeNode.Stroke;
            UimStyle style = uimStroke.Style;

            if (!inkModel.Brushes.TryGetBrush(style.BrushUri, out UimBrush brush))
                return null;

            if (brush is UimVectorBrush)
            {
                return new VectorStroke(
                    node.Id,
                    uimStroke.Spline.ToSpline(),
                    m_brushes[brush.Name],
                    style.PathPointProperties,
                    AppBrush.RenderModeUriToBlendMode(style.RenderModeUri));
            }
            else if (brush is UimRasterBrush)
            {
                return new RasterStroke(
                    node.Id,
                    uimStroke.Spline.ToSpline(),
                    m_brushes[brush.Name],
                    style.PathPointProperties,
					AppBrush.RenderModeUriToBlendMode(style.RenderModeUri),
                    uimStroke.RandomSeed);
            }

            throw new Exception("Unsupported brush type");
        }

        public StrokeNode StrokeToNode(Stroke stroke)
        {
            bool enableDeltaCompression = false;
            uint randomSeed = stroke is RasterStroke rasterStroke ? rasterStroke.RandomSeed : 0;

            UimStyle style = new UimStyle(stroke.Brush.BrushUri);
            style.PathPointProperties.Alpha = stroke.Alpha;
            style.PathPointProperties.Red = stroke.Red;
            style.PathPointProperties.Green = stroke.Green;
            style.PathPointProperties.Blue = stroke.Blue;
            style.PathPointProperties.Size = stroke.Size;
            style.PathPointProperties.Rotation = stroke.Rotation;
            style.PathPointProperties.OffsetX = stroke.OffsetX;
            style.PathPointProperties.OffsetY = stroke.OffsetY;
            style.PathPointProperties.OffsetZ = stroke.OffsetZ;
            style.PathPointProperties.ScaleX = stroke.ScaleX;
            style.PathPointProperties.ScaleY = stroke.ScaleY;
            style.PathPointProperties.ScaleZ = stroke.ScaleZ;

            UimStroke ss = new UimStroke(stroke.Id, stroke.Spline, style, null, 0, null, randomSeed);

            if (enableDeltaCompression)
            {
                ss.PrecisionScheme = new PrecisionScheme(
                    positionPrecision: 2,
                    sizePrecision: 2,
                    rotationPrecision: 2,
                    scalePrecision: 2,
                    offsetPrecision: 2);
            }

            return new StrokeNode(ss);
        }

        internal async Task LoadStrokesFromModel(InkModel inkModel, Graphics graphics)
        {
            Clear();

            foreach (var vb in inkModel.Brushes.VectorBrushes)
            {
                RegisterVectorBrush(vb);
            }

            foreach (var rb in inkModel.Brushes.RasterBrushes)
            {
                await RegisterRasterBrushAsync(rb, graphics);
            }

            if (inkModel.InkTree.Root != null)
            {
                IEnumerator<InkNode> enumerator = inkModel.InkTree.Root.GetRecursiveEnumerator();

                while (enumerator.MoveNext())
                {
                    Stroke stroke = NodeToStroke(inkModel, enumerator.Current);

                    if (stroke != null)
                    {
                        Strokes.Add(stroke);
                        AddStrokeToSpatialModel(stroke);
                    }
                }
            }
        }

        public InkModel BuildUniversalInkModelFromCanvasStrokes()
        {
            InkModel inkModel = new InkModel();

            inkModel.InkTree.Root = new StrokeGroupNode(Identifier.FromNewGuid());

            foreach (Stroke stroke in Strokes)
            {
                StrokeNode strokeNode = StrokeToNode(stroke);

                if (strokeNode != null)
                {
                    inkModel.InkTree.Root.Add(strokeNode);
                }
            }

            return inkModel;
        }

        public async Task BuildUIMBrushesFromAppBrushes(InkModel inkModel)
        {
            foreach (Stroke stroke in Strokes)
            {
                if (stroke is VectorStroke vectorStroke)
                {
                    UimVectorBrush vectorBrush = new UimVectorBrush(vectorStroke.Brush.BrushUri, vectorStroke.Brush.VectorBrush.Polygons);

                    if (!inkModel.Brushes.TryGetBrush(vectorBrush.Name, out UimBrush _))
                    {
                        inkModel.Brushes.AddVectorBrush(vectorBrush);
                    }
                }
                else if (stroke is RasterStroke rasterStroke)
                {
                    AppRasterBrush appRasterBrush = (AppRasterBrush)rasterStroke.Brush;

                    ParticleBrush particleBrush = appRasterBrush.ParticleBrush;

                    byte[] fillTextureBytes = await appRasterBrush.m_fillTexturePngs.GetPngBytesAsync();

                    List<byte[]> shapeTexturesBytes = new List<byte[]>();
                    foreach (var provider in appRasterBrush.m_shapeTexturesPngs)
                    {
                        shapeTexturesBytes.Add(await provider.GetPngBytesAsync());
                    }

                    RasterBrush rasterBrush = new RasterBrush(
                        rasterStroke.Brush.BrushUri,
                        (float)particleBrush.FillTileSize.Width,
                        (float)particleBrush.FillTileSize.Height,
                        false,
                        (RotationMode)particleBrush.RotationMode,
                        particleBrush.Scattering,
                        appRasterBrush.Spacing,
                        fillTextureBytes,
                        shapeTexturesBytes,
                        (UimBlendMode)rasterStroke.Brush.BlendMode);

                    if (!inkModel.Brushes.TryGetBrush(rasterBrush.Name, out UimBrush _))
                    {
                        inkModel.Brushes.AddRasterBrush(rasterBrush);
                    }
                }
            }
        }

        public void StoreStroke(Stroke stroke, int atIndex = -1)
        {
            int newStrokePointsCount = stroke.GetSplineControlPointsCount();

            if (newStrokePointsCount < 4)
            {
                throw new Exception($"Invalid stroke with {newStrokePointsCount} points");
            }

            if (atIndex == -1)
            {
                atIndex = Strokes.Count;
            }

            Strokes.Insert(atIndex, stroke);
            AddStrokeToSpatialModel(stroke);

            m_brushes.TryAdd(stroke.Brush.BrushUri, stroke.Brush);
        }

        public void RemoveStroke(int strokeIndex)
        {
            Stroke stroke = Strokes[strokeIndex];

            m_spatialModel.Remove(stroke);

            Strokes.RemoveAt(strokeIndex);
        }

        public int FindStrokeIndex(Identifier id)
        {
            int index = -1;

            foreach (var stroke in Strokes)
            {
                index++;

                if (stroke.Id == id)
                    return index;
            }

            return -1;
        }

        public void MoveSelectedStrokes(Matrix3x2 transform, Selection selection)
        {
            foreach (Stroke stroke in Strokes.Where(s => selection.Contains(s.Id)))
            {
                if (stroke is VectorStroke vectorStroke)
                {
                    m_spatialModel.Remove(stroke);
                    Stroke.TransformPath(vectorStroke.Spline.Path, transform, 1f);
                    stroke.RebuildCache();
                    m_spatialModel.TryAdd(vectorStroke);
                }
            }
        }

        public void RebuildStrokesCache()
        {
            foreach (var stroke in Strokes)
            {
                stroke.RebuildCache();
            }
        }

        public void RegisterVectorBrush(UimVectorBrush uimVectorBrush)
        {
            if (m_brushes.TryGetValue(uimVectorBrush.Name, out _))
            {
                return;
            }

            GeometryVectorBrush vectorBrush;
            if (uimVectorBrush.BrushPolygons.Count == 0)
            {
                vectorBrush = VectorBrushGeometries.Circle;
            }
            else
            {
                vectorBrush = new GeometryVectorBrush(uimVectorBrush.BrushPolygons);
            }

            AppBrush vectorPaint = new AppBrush(uimVectorBrush.Name, vectorBrush);
            m_brushes.Add(uimVectorBrush.Name, vectorPaint);
        }

        public async Task RegisterRasterBrushAsync(UimRasterBrush uimRasterBrush, Graphics graphics)
        {
            if (m_brushes.TryGetValue(uimRasterBrush.Name, out _))
            {
                return;
            }

            int mipMapLevelsCount = uimRasterBrush.ShapeTextures.Count;

            PngDataFromByteArrayProvider[] shapeTextureProviders = new PngDataFromByteArrayProvider[mipMapLevelsCount];

            int k = 0;

            foreach (byte[] shapeTexturePngData in uimRasterBrush.ShapeTextures)
            {
                shapeTextureProviders[k++] = new PngDataFromByteArrayProvider(shapeTexturePngData);
            }

            AppRasterBrush appRasterBrush = new AppRasterBrush(
                uimRasterBrush.Name,
                uimRasterBrush.Spacing,
                uimRasterBrush.Scattering,
                (Wacom.Ink.Rendering.BlendMode)uimRasterBrush.BlendMode,
                VectorBrushGeometries.Circle,
                new Size(uimRasterBrush.FillWidth, uimRasterBrush.FillHeight),
                (ParticleRotationMode)uimRasterBrush.RotationMode,
                new PngDataFromByteArrayProvider(uimRasterBrush.FillTexture),
                shapeTextureProviders);

            await appRasterBrush.LoadBrushTexturesAsync(graphics);

            m_brushes.Add(uimRasterBrush.Name, appRasterBrush);
        }

        public EraseStrokePartManipulation CreateEraseStrokePartOperation() => new EraseStrokePartManipulation(m_spatialModel);

        public EraseWholeStrokeManipulation CreateEraseWholeStrokeOperation() => new EraseWholeStrokeManipulation(m_spatialModel);

        public SelectStrokePartManipulation CreateSelectStrokePartOperation() => new SelectStrokePartManipulation(m_spatialModel);

        public SelectWholeStrokeManipulation CreateSelectWholeStrokeOperation() => new SelectWholeStrokeManipulation(m_spatialModel);

        #endregion

        #region Implementation

        private void AddStrokeToSpatialModel(Stroke stroke)
        {
            // Manipulations of raster strokes are not supported yet
            if (stroke is VectorStroke)
            {
                m_spatialModel.TryAdd(stroke);
            }
        }

        #endregion
    }
}
