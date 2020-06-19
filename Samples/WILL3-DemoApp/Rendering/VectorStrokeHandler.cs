using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Rendering;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulation;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

    public class VectorStrokeHandler : StrokeHandler
    {
        #region Fields

        /// <summary>
        /// List of completed strokes
        /// </summary>
        private List<VectorInkStroke> m_dryStrokes = new List<VectorInkStroke>();

        private Polygon m_addedPolygon = new Polygon();
        private Polygon m_predictedPolygon = new Polygon();

        private VectorDrawingTool ActiveTool = null;
        private VectorSelectionTool mSelectTool;
        private SpatialModel m_spatialModel = new SpatialModel(new VectorInkStrokeFactory());
        private IAsyncAction m_spatialModelLoopWorker;
        private VectorDrawingTool mSavedTool;
        private MediaColor mSavedColor;


        #endregion

        #region Properties

        public VectorInkBuilder VectorInkBuilder => ActiveTool.InkBuilder;


        public override BrushType BrushType { get { return BrushType.Vector; } }
        public override MediaColor BrushColor { get; set; }

        public override InkBuilder InkBuilder => VectorInkBuilder;

        public override bool IsSelecting => ActiveTool is VectorSelectionTool;

        public override IEnumerable<Identifier> SelectedStrokes => mSelectTool?.SelectedStrokes;

        #endregion

        #region Constructor

        public VectorStrokeHandler(Renderer renderer, VectorBrushStyle style, MediaColor color)
          : base(renderer, color)
        {
            BrushColor = color;
            SetBrushStyle(style);

            // Spatial Model
            m_spatialModel = new SpatialModel(new VectorInkStrokeFactory());
            m_spatialModel.StrokeAdded += OnSpatialModelStrokeAdded;
            m_spatialModel.StrokeRemoved += OnSpatialModelStrokeRemoved;
            m_spatialModel.StrokeSelected += OnSpatialModelStrokeSelected;
            m_spatialModel.EraseFinished += OnSpatialModelEraseFinished;
            m_spatialModel.SelectStarted += OnSpatialModelSelectStarted;
            m_spatialModel.SelectFinished += OnSpatialModelSelectFinished;

            WorkItemHandler workItemHandler = new WorkItemHandler((IAsyncAction action) =>
            {
                m_spatialModel.StartProcessingJobs();
            });

            m_spatialModelLoopWorker = Windows.System.Threading.ThreadPool.RunAsync(workItemHandler, WorkItemPriority.High, WorkItemOptions.TimeSliced);
        }

        #endregion


        #region Public Interface

        private VectorBrushStyle mBrushStyle = VectorBrushStyle.Pen;
        public override void SetBrushStyle(VectorBrushStyle value)
        {
            StopSelectionMode();
            switch (mBrushStyle = value)
            {
                case VectorBrushStyle.Pen:
                    ActiveTool = new PenTool();
                    break;
                case VectorBrushStyle.Felt:
                    ActiveTool = new FeltTool();
                    break;
                case VectorBrushStyle.Brush:
                    ActiveTool = new BrushTool();
                    break;
                case VectorBrushStyle.Selection:
                    throw new InvalidOperationException("");
                default:
                    throw new Exception("Unknown brush type");
            }
            ActiveTool.PointsAdded += OnPointsAdded;
        }

        public override void StartSelectionMode()
        {
            mSavedTool = ActiveTool;
            mSavedColor = BrushColor;
            ActiveTool = mSelectTool = new VectorSelectionTool(this);
            mSelectTool.DrawingFinished += OnSelectFinished;
            mSelectTool.PointsAdded += OnPointsAdded;
            mSelectTool.OnTranslate += OnToolTranslate;
            mSelectTool.TranslateFinished += OnToolTranslateFinished;

            BrushColor = MediaColor.FromArgb(96, 0, 0, 0);

        }

        public override void StopSelectionMode()
        {
            if (IsSelecting)
            {
                ActiveTool = mSavedTool;
                BrushColor = mSavedColor;
            }
        }

        /// <summary>
        /// Brush-specific handling of GraphicsReady event
        /// </summary>
        public override void DoGraphicsReady()
        {
        }

        /// <summary>
        /// Draw all saved strokes
        /// </summary>
        /// <param name="renderingContext">RenderingContext to draw to</param>
        /// <param name="o">Cached stroke (as object)</param>
        public override void DoRenderStroke(RenderingContext renderingContext, object o, bool translationLayerPainted)
        {
            VectorInkStroke stroke = (VectorInkStroke)o;
            renderingContext.FillPolygon(stroke.Polygon, stroke.Color, Ink.Rendering.BlendMode.SourceOver);
        }

        public override void DrawTranslation(RenderingContext renderingContext, Layer translationLayer)
        {
            renderingContext.DrawLayer(translationLayer, mSelectTool.SourceRect, mSelectTool.DestRect, Wacom.Ink.Rendering.BlendMode.SourceOver);
        }

        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public override void ClearStrokes()
        {
            if (mSelectTool != null && mSelectTool.SelectedStrokes.Count > 0)
            {
                foreach (var strokeId in mSelectTool.SelectedStrokes)
                {
                    m_spatialModel.Remove(strokeId);
                    m_dryStrokes.Remove(m_dryStrokes.Find(stroke => stroke.Equals(strokeId)));
                }
                mSelectTool.SelectedStrokes.Clear();
                mRenderer.RedrawAllStrokes(null, null);
            }
            else
            {
                foreach (var stroke in m_dryStrokes)
                {
                    m_spatialModel.Remove(stroke.Id);
                }
                m_dryStrokes.Clear();
            }
            mSerializer = new Serializer();
        }

        /// <summary>
        /// Make the current stroke permanent
        /// </summary>
        /// <remarks>Copies the output of the render pipeline from InkBuilder to dry strokes</remarks>
        public override void StoreCurrentStroke(PointerDeviceType deviceType)
        {
            var polygons = VectorInkBuilder.PolygonSimplifier.AllData;
            var mergedPolygons = PolygonUtils.MergePolygons(polygons);

            var stroke = new VectorInkStroke(deviceType, VectorInkBuilder, BrushColor, mergedPolygons, ActiveTool.Shape, mSerializer.AddSensorData(deviceType, VectorInkBuilder.GetPointerDataList()));

            m_dryStrokes.Add(stroke);
            m_spatialModel.Add(stroke);
        }

        public override InkModel Serialize()
        {
            mSerializer.Init();
            foreach (var stroke in m_dryStrokes)
            {
                mSerializer.EncodeStroke(stroke);
            }
            return mSerializer.InkDocument;
        }

        public override void RenderAllStrokes(RenderingContext context, IEnumerable<Identifier> excluded, Rect? clipRect)
        {
            foreach (var stroke in m_dryStrokes)
            {
                if (excluded == null || !excluded.Contains(stroke.Id))
                {
                    // Draw current stroke
                    context.SetTarget(mRenderer.CurrentStrokeLayer);
                    context.ClearColor(Colors.Transparent);

                    DoRenderStroke(context, stroke, mRenderer.TranslationLayerPainted);

                    // Blend stroke to Scene Layer
                    context.SetTarget(mRenderer.SceneLayer);
                    context.DrawLayer(mRenderer.CurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

                    // Blend Current Stroke to All Strokes Layer
                    context.SetTarget(mRenderer.AllStrokesLayer);
                    context.DrawLayer(mRenderer.CurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);
                }
            }
        }

        /// <summary>
        /// Handles brush-specific parts of drawing a new stroke segment
        /// </summary>
        /// <param name="updateRect">returns bounding rectangle of area requiring update</param>
        public override void DoRenderNewStrokeSegment(out Rect updateRect)
        {
            var result = ActiveTool.Polygons;

            ConvertPolygon(result.Addition, m_addedPolygon);
            ConvertPolygon(result.Prediction, m_predictedPolygon);

            // Draw the added stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.CurrentStrokeLayer);
            Rect addedStrokeRect = mRenderer.RenderingContext.FillPolygon(m_addedPolygon, BrushColor, Ink.Rendering.BlendMode.Max);

            // Measure the predicted stroke
            Rect predictedStrokeRect = mRenderer.RenderingContext.MeasurePolygonBounds(m_predictedPolygon);

            // Calculate the update rect for this frame
            updateRect = mRenderer.DirtyRectManager.GetUpdateRect(addedStrokeRect, predictedStrokeRect);

            // Draw the predicted stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.PrelimPathLayer);
            mRenderer.RenderingContext.DrawLayerAtPoint(mRenderer.CurrentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.Copy);
            mRenderer.RenderingContext.FillPolygon(m_predictedPolygon, BrushColor, Ink.Rendering.BlendMode.Max);
        }

        public override Rect DoRenderSelectedStrokes(RenderingContext renderingCtx, IEnumerable<Identifier> selectedStrokeIds)
        {
            if (!IsSelecting)
            {
                throw new InvalidOperationException("Unexpected call to RenderSelectedStrokes");
            }

            Rect rect = Rect.Empty;

            if (selectedStrokeIds.Count() == 0)
            {

                mSelectTool.DestRect = rect;
                mSelectTool.SourceRect = rect;

                return rect;
            }

            foreach (var id in selectedStrokeIds)
            {
                var dryStroke = m_dryStrokes.Find(x => x.Equals(id));

                Rect polyBounds = renderingCtx.FillPolygon(dryStroke.Polygon, dryStroke.Color, Wacom.Ink.Rendering.BlendMode.SourceOver);

                if (rect.IsEmpty)
                    rect = polyBounds;
                else
                    rect.Union(polyBounds);

                mSelectTool.DestRect = rect;
                mSelectTool.SourceRect = rect;

            }
            return rect;
        }

        /// <summary>
        /// Loads serialized ink
        /// </summary>
        /// <param name="inkDocument"></param>
        public override void LoadInk(InkModel inkDocument)
        {
            base.LoadInk(inkDocument);
            m_dryStrokes = new List<VectorInkStroke>(RecreateDryStrokes(inkDocument));
            
        }

        #endregion

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            base.OnPressed(uiElement, args);
            ActiveTool.OnPressed(uiElement, args);
        }
        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            ActiveTool.OnMoved(uiElement, args);
        }
        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            ActiveTool.OnReleased(uiElement, args);
        }

        #region DrawingTool


        public override void SetupStrokeTool(Windows.Devices.Input.PointerDevice device)
        {
            (PathPointLayout, Calculator) layoutAndCalc;

            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutAndCalc = ActiveTool.GetLayoutAndCalulatorForMouse();
                    break;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutAndCalc = ActiveTool.GetLayoutAndCalulatorForStylus();
                    break;

                default:
                    throw new Exception("Unknown input device type");
            }
            VectorInkBuilder.UpdatePipeline(layoutAndCalc.Item1, layoutAndCalc.Item2, ActiveTool.Shape);
        }

#endregion

#region Event Handlers

        public void OnPointsAdded(object sender, EventArgs args)
        {
            mRenderer.RenderNewStrokeSegment();
            mRenderer.RenderBackbuffer();
            mRenderer.PresentGraphics();
        }

        public void OnSelectFinished(object sender, bool blendCurrentStroke)
        {
            List <List<Vector2>> clonedSimpl = new List<List<Vector2>>(VectorInkBuilder.PolygonSimplifier.AllData);
            List<List<Vector2>> mergedPolygons = PolygonUtils.MergePolygons(clonedSimpl);

            if (!TransformationMatrix.IsIdentity)
            {
                bool res = Matrix3x2.Invert(TransformationMatrix, out Matrix3x2 viewToModelTransformationMatrix);

                if (!res)
                {
                    throw new InvalidOperationException("Transform matrix could not be inverted.");
                }

                TransformUtils.TransformPolysXY(mergedPolygons, viewToModelTransformationMatrix);
            }

            m_spatialModel.Select(mergedPolygons[0], ManipulationMode.PartialStroke);
        }

        #endregion

        #region IDispose support
        public override void Dispose()
        {
            m_spatialModel.StopProcessingJobs();

            m_dryStrokes.Clear();
        }

#endregion

#region Implementation

        private static void ConvertPolygon(List<PolygonVertices> src, Polygon dest)
        {
            dest.RemoveAllContours();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }
        }

        #endregion


        #region Spatial Model Event Handlers

        private void OnSpatialModelStrokeAdded(object sender, IInkStroke stroke)
        {
            VectorSplineInkBuilder inkBuilder = new VectorSplineInkBuilder();
            var result = inkBuilder.AddWholePath(stroke.Spline, stroke.Layout, stroke.VectorBrush);

            var vectStroke = (VectorInkStroke)stroke;
            vectStroke.SimplPoly = result.Merged.Addition;
            vectStroke.Polygon = PolygonUtil.ConvertPolygon(vectStroke.SimplPoly);

            m_dryStrokes.Add(vectStroke);
        }

        private void OnSpatialModelStrokeRemoved(object sender, Identifier strokeId)
        {
            var dryStroke = m_dryStrokes.Find(x => x.Equals(strokeId));
            m_dryStrokes.Remove(dryStroke);
        }

        private void OnSpatialModelStrokeSelected(object sender, Identifier strokeId)
        {
            mSelectTool.SelectedStrokes.Add(strokeId);
        }

        private void OnSpatialModelEraseFinished(object sender, EventArgs e)
        {
        }

        private void OnSpatialModelSelectStarted(object sender, EventArgs e)
        {
            mSelectTool.SelectedStrokes.Clear();
        }

        private void OnSpatialModelSelectFinished(object sender, EventArgs e)
        {
            mRenderer.InvokeRenderSelected(mSelectTool.SelectedStrokes);
        }

        private void OnToolTranslate(object sender, EventArgs args)
        {
            mRenderer.RenderBackbuffer();
            mRenderer.PresentGraphics();
        }

        private void OnToolTranslateFinished(object sender, Matrix3x2 transform)
        {
            foreach (var selectedId in mSelectTool.SelectedStrokes)
            {
                VectorInkStroke stroke = m_dryStrokes.Find(x => x.Equals(selectedId));

                Spline transformedSpline = TransformUtils.TransformSplineXY(stroke.Spline, stroke.Layout, transform);

                m_spatialModel.Remove(selectedId);

                stroke.UpdateSpline(transformedSpline);

                m_spatialModel.Add(stroke);
            }

            mRenderer.InvokeRenderSelected(mSelectTool.SelectedStrokes);
        }

        #endregion

        #region Serialization Support

        private List<VectorInkStroke> RecreateDryStrokes(InkModel inkDataModel)
        {
            if (inkDataModel.InkTree.Root == null)
                return new List<VectorInkStroke>();

            List<VectorInkStroke> dryStrokes = new List<VectorInkStroke>(inkDataModel.Strokes.Count);

            DecodedVectorInkBuilder decodedVectorInkBuilder = new DecodedVectorInkBuilder();

            foreach (var stroke in inkDataModel.Strokes)
            {
                var vectorInkStroke = CreateDryStroke(decodedVectorInkBuilder, stroke, inkDataModel);
                dryStrokes.Add(vectorInkStroke);
                m_spatialModel.Add(vectorInkStroke);

                bool res = inkDataModel.SensorData.GetSensorData(vectorInkStroke.SensorDataId, out SensorData sensorData);

                if (res)
                {
                    mSerializer.LoadSensorDataFromModel(inkDataModel, sensorData);
                }
            }

            return dryStrokes;
        }

        private VectorInkStroke CreateDryStroke(DecodedVectorInkBuilder decodedVectorInkBuilder, Stroke stroke, InkModel inkDataModel)
        {
            inkDataModel.Brushes.TryGetBrush(stroke.Style.BrushUri, out Wacom.Ink.Serialization.Model.Brush brush);

            if (brush is Wacom.Ink.Serialization.Model.VectorBrush vectorBrush)
            {
                return CreateDryStrokeFromVectorBrush(decodedVectorInkBuilder, vectorBrush, stroke);
            }
            else if (brush is RasterBrush rasterBrush)
            {
                throw new Exception("This sample does not support serialization of both raster and vector brushes");
            }
            else
            {
                throw new Exception("Brush not recognized");
            }
        }

        private VectorInkStroke CreateDryStrokeFromVectorBrush(DecodedVectorInkBuilder decodedVectorInkBuilder, Wacom.Ink.Serialization.Model.VectorBrush vectorBrush, Stroke stroke)
        {
            Wacom.Ink.Geometry.VectorBrush vb = new Wacom.Ink.Geometry.VectorBrush(vectorBrush.BrushPolygons.ToArray());
            var pipelineData = decodedVectorInkBuilder.AddWholePath(stroke.Spline, stroke.Layout, vb);

            return new VectorInkStroke(stroke, vectorBrush, pipelineData);
        }

        private class DecodedVectorInkBuilder
        {
            private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
            private PolygonMerger mPolygonMerger = new PolygonMerger();
            private readonly PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

            public PipelineData AddWholePath(Spline path, PathPointLayout layout, Wacom.Ink.Geometry.VectorBrush vectorBrush)
            {
                var splineInterpolator = new CurvatureBasedInterpolator(layout);
                var brushApplier = new BrushApplier(layout, vectorBrush);

                var points = splineInterpolator.Add(true, true, path, null);

                var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);

                var hulls = mConvexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);

                var merged = mPolygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);

                return new PipelineData(polys, merged);
            }
        }

        #endregion

    }
}