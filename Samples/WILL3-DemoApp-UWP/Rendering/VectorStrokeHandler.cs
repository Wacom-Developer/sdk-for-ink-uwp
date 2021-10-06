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
        private List<VectorInkStroke> mDryStrokes = new List<VectorInkStroke>();
        private object mDryStrokeLock = new object();

        private Polygon mAddedPolygon = new Polygon();
        private Polygon mPredictedPolygon = new Polygon();

        private VectorDrawingTool mActiveTool = null;
        private VectorSelectionTool mSelectTool = null;
       
        private SpatialModel mSpatialModel = new SpatialModel(new VectorInkStrokeFactory());
        private IAsyncAction mSpatialModelLoopWorker;
        private MediaColor mSavedColor;


        #endregion

        #region Properties

        public VectorInkBuilder VectorInkBuilder => mActiveTool.InkBuilder;


        public override BrushType BrushType { get { return BrushType.Vector; } }
        public override MediaColor BrushColor { get; set; }

        //public override VectorInkBuilder InkBuilder => VectorInkBuilder;

        public override bool IsSelecting => mSelectTool != null;// ActiveTool is VectorManipulationTool;

        public override IEnumerable<Identifier> SelectedStrokes => mSelectTool?.SelectedStrokes;

        #endregion

        #region Constructor

        public VectorStrokeHandler(Renderer renderer, VectorBrushStyle style, MediaColor color)
          : base(renderer, color)
        {
            BrushColor = color;
            SetBrushStyle(style);

            // Spatial Model
            mSpatialModel = new SpatialModel(new VectorInkStrokeFactory());
            mSpatialModel.StrokeAdded += OnSpatialModelStrokeAdded;
            mSpatialModel.StrokeRemoved += OnSpatialModelStrokeRemoved;
            mSpatialModel.StrokeSelected += OnSpatialModelStrokeSelected;
            mSpatialModel.EraseFinished += OnSpatialModelEraseFinished;
            mSpatialModel.SelectStarted += OnSpatialModelSelectStarted;
            mSpatialModel.SelectFinished += OnSpatialModelSelectFinished;

            WorkItemHandler workItemHandler = new WorkItemHandler((IAsyncAction action) =>
            {
                try
                {
                    mSpatialModel.StartProcessingJobs();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"Exception: {ex.Message}");
                }
            });

            mSpatialModelLoopWorker = Windows.System.Threading.ThreadPool.RunAsync(workItemHandler, WorkItemPriority.High, WorkItemOptions.TimeSliced);
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
                    mActiveTool = new PenTool();
                    break;
                case VectorBrushStyle.Felt:
                    mActiveTool = new FeltTool();
                    break;
                case VectorBrushStyle.Brush:
                    mActiveTool = new BrushTool();
                    break;
                case VectorBrushStyle.Selection:
                    throw new InvalidOperationException("");
                default:
                    throw new Exception("Unknown brush type");
            }
            mActiveTool.PointsAdded += OnPointsAdded;
        }

        public override void StartSelectionMode(SelectionMode mode)
        {
            mSavedColor = BrushColor;

            if (mSelectTool != null && mSelectTool.SelectedStrokes.Count > 0)
            {
                mRenderer.InvokeRedrawAllStrokes();
            }

            ManipulationMode manipulation = (mode & SelectionMode.Whole) != 0 ? ManipulationMode.WholeStroke : ManipulationMode.PartialStroke;
            if ((mode & SelectionMode.Manipulate) != 0)
            {
                var manipulationTool = new VectorManipulationTool(this, manipulation);
                manipulationTool.OnTranslate += OnToolTranslate;
                manipulationTool.TranslateFinished += OnToolTranslateFinished;
                manipulationTool.DrawingFinished += OnManipulationSelectFinished;
                mSelectTool = manipulationTool;
            }
            else if ((mode & SelectionMode.Erase) != 0)
            {
                var eraseTool = new VectorEraserTool(manipulation);
                eraseTool.DrawingFinished += OnEraseSelectFinished;
                mSelectTool = eraseTool;
            }

            mSelectTool.PointsAdded += OnPointsAdded;
            mActiveTool = mSelectTool;

            BrushColor = MediaColor.FromArgb(96, 0, 0, 0);

        }

        public override void StopSelectionMode()
        {
            if (IsSelecting)
            {
                mActiveTool = null;
                BrushColor = mSavedColor;
                mSelectTool = null;
                mRenderer.InvokeRedrawAllStrokes();
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
            var manipulationTool = mSelectTool as VectorManipulationTool;
            renderingContext.DrawLayer(translationLayer, manipulationTool.SourceRect, manipulationTool.DestRect, Wacom.Ink.Rendering.BlendMode.SourceOver);
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
                    mSpatialModel.Remove(strokeId);
                    mDryStrokes.Remove(mDryStrokes.Find(stroke => stroke.Equals(strokeId)));
                }
                mSelectTool.SelectedStrokes.Clear();
                mRenderer.RedrawAllStrokes(null, null);
            }
            else
            {
                mSpatialModel.Reset();
                mDryStrokes.Clear();
            }
            mSerializer = new Serializer();
        }

        /// <summary>
        /// Make the current stroke permanent
        /// </summary>
        /// <remarks>Copies the output of the render pipeline from InkBuilder to dry strokes</remarks>
        public override void StoreCurrentStroke(PointerDeviceType deviceType)
        {
            var strokePolygon = VectorInkBuilder.CreateStrokePolygon();

            var stroke = new VectorInkStroke(deviceType, VectorInkBuilder, BrushColor, strokePolygon, mActiveTool.Shape, mSerializer.AddSensorData(deviceType, VectorInkBuilder.GetPointerDataList()));

            mDryStrokes.Add(stroke);
            mSpatialModel.Add(stroke);
        }

        public override InkModel Serialize()
        {
            mSerializer.Init();
            foreach (var stroke in mDryStrokes)
            {
                mSerializer.EncodeStroke(stroke);
            }
            return mSerializer.InkDocument;
        }

        public override void RenderAllStrokes(RenderingContext context, IEnumerable<Identifier> excluded, Rect? clipRect)
        {
            lock (mDryStrokeLock)
            {
                foreach (var stroke in mDryStrokes)
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

            }        }

        /// <summary>
        /// Handles brush-specific parts of drawing a new stroke segment
        /// </summary>
        /// <param name="updateRect">returns bounding rectangle of area requiring update</param>
        public override void DoRenderNewStrokeSegment(out Rect updateRect)
        {
            var result = mActiveTool.Polygons;

            ConvertPolygon(result.Addition, mAddedPolygon);
            ConvertPolygon(result.Prediction, mPredictedPolygon);

            // Draw the added stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.CurrentStrokeLayer);
            Rect addedStrokeRect = mRenderer.RenderingContext.FillPolygon(mAddedPolygon, BrushColor, Ink.Rendering.BlendMode.Max);

            // Measure the predicted stroke
            Rect predictedStrokeRect = mRenderer.RenderingContext.MeasurePolygonBounds(mPredictedPolygon);

            // Calculate the update rect for this frame
            updateRect = mRenderer.DirtyRectManager.GetUpdateRect(addedStrokeRect, predictedStrokeRect);

            // Draw the predicted stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.PrelimPathLayer);
            mRenderer.RenderingContext.DrawLayerAtPoint(mRenderer.CurrentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.Copy);
            mRenderer.RenderingContext.FillPolygon(mPredictedPolygon, BrushColor, Ink.Rendering.BlendMode.Max);
        }

        public override Rect DoRenderSelectedStrokes(RenderingContext renderingCtx, IEnumerable<Identifier> selectedStrokeIds)
        {
            if (!IsSelecting)
            {
                throw new InvalidOperationException("Unexpected call to RenderSelectedStrokes");
            }

            Rect rect = Rect.Empty;

            var manipulationTool = mSelectTool as VectorManipulationTool;
            if (selectedStrokeIds.Count() == 0)
            {

                manipulationTool.DestRect = rect;
                manipulationTool.SourceRect = rect;

                return rect;
            }

            lock (mDryStrokeLock)
            {
                foreach (var id in selectedStrokeIds)
                {
                    var dryStroke = mDryStrokes.Find(x => x.Equals(id));

                    if (dryStroke == null)
                    {
                        continue;
                    }

                    Rect polyBounds = renderingCtx.FillPolygon(dryStroke.Polygon, dryStroke.Color, Wacom.Ink.Rendering.BlendMode.SourceOver);

                    if (rect.IsEmpty)
                        rect = polyBounds;
                    else
                        rect.Union(polyBounds);

                    manipulationTool.DestRect = rect;
                    manipulationTool.SourceRect = rect;

                }

            }
            return rect;
        }

        /// <summary>
        /// Loads serialized ink
        /// </summary>
        /// <param name="inkDocument"></param>
        public override void LoadInk(InkModel inkDocument)
        {
            mSpatialModel.Reset();
            mSerializer = new Serializer();

            base.LoadInk(inkDocument);
            mDryStrokes = new List<VectorInkStroke>(RecreateDryStrokes(inkDocument));
        }

        #endregion

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            base.OnPressed(uiElement, args);
            mActiveTool.OnPressed(uiElement, args);
        }
        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            mActiveTool.OnMoved(uiElement, args);
        }
        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            mActiveTool.OnReleased(uiElement, args);
        }

        #region DrawingTool


        public override void SetupStrokeTool(Windows.Devices.Input.PointerDevice device)
        {
            LayoutMask layout = mActiveTool.GetLayout(device.PointerDeviceType);
            Calculator calculator = mActiveTool.GetCalculator(device.PointerDeviceType);

            VectorInkBuilder.UpdatePipeline(layout, calculator, mActiveTool.Shape);
        }

        #endregion

        #region Event Handlers

        public void OnPointsAdded(object sender, EventArgs args)
        {
            mRenderer.RenderNewStrokeSegment();
            mRenderer.RenderBackbuffer();
            mRenderer.PresentGraphics();
        }

        public void OnManipulationSelectFinished(object sender, bool blendCurrentStroke)
        {
            List<List<Vector2>> clonedSimpl = new List<List<Vector2>>(VectorInkBuilder.PolygonAccumulator.Accumulated);
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

            // NOTE: If ManipulationMode is PartialStroke, affected strokes are deleted and new strokes created
            //       for selected and non-selected portions. This occurs BEFORE any manipulation takes place.
            //       Reverting to previous state in the event of no manipulation taking place requires 
            //       undo functionality (delete added strokes, restore deleted strokes)
            mSpatialModel.Select(mergedPolygons[0], mSelectTool.ManipulationMode);
        }

        public void OnEraseSelectFinished(object sender, bool blendCurrentStroke)
        {
            List<List<Vector2>> convexHulls = new List<List<Vector2>>(VectorInkBuilder.ConvexHullChainProducer.AllData);


            if (!TransformationMatrix.IsIdentity)
            {
                bool res = Matrix3x2.Invert(TransformationMatrix, out Matrix3x2 viewToModelTransformationMatrix);

                if (!res)
                {
                    throw new InvalidOperationException("Transform matrix could not be inverted.");
                }

                TransformUtils.TransformPolysXY(convexHulls, viewToModelTransformationMatrix);
            }

            mSpatialModel.Erase(convexHulls, mSelectTool.ManipulationMode);
        }

        #endregion

        #region IDispose support
        public override void Dispose()
        {
            mSpatialModel.StopProcessingJobs();

            mDryStrokes.Clear();
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
            var result = inkBuilder.AddWholePath(stroke.Spline, stroke.VectorBrush);

            var vectStroke = (VectorInkStroke)stroke;
            vectStroke.SimplPoly = result.Merged.Addition;
            vectStroke.Polygon = PolygonUtil.ConvertPolygon(vectStroke.SimplPoly);


            lock (mDryStrokeLock)
            {
                mDryStrokes.Add(vectStroke); 
            }
        }

        private void OnSpatialModelStrokeRemoved(object sender, Identifier strokeId)
        {
            lock (mDryStrokeLock)
            {
                var dryStroke = mDryStrokes.Find(x => x.Equals(strokeId));
                mDryStrokes.Remove(dryStroke);

            }
        }

        private void OnSpatialModelStrokeSelected(object sender, Identifier strokeId)
        {
            mSelectTool.SelectedStrokes.Add(strokeId);
        }

        private void OnSpatialModelEraseFinished(object sender, EventArgs e)
        {
            mRenderer.InvokeRedrawAllStrokes();
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
            lock (mDryStrokeLock)
            {
                foreach (var selectedId in mSelectTool.SelectedStrokes)
                {
                    VectorInkStroke stroke = mDryStrokes.Find(x => x.Equals(selectedId));

                    if (stroke == null)
                    {
                        continue;
                    }

                    Spline transformedSpline = TransformUtils.TransformSplineXY(stroke.Spline, transform);

                    mSpatialModel.Remove(selectedId);

                    stroke.UpdateSpline(transformedSpline);

                    mSpatialModel.Add(stroke);
                }

                mRenderer.InvokeRenderSelected(mSelectTool.SelectedStrokes); 
            }
        }

        #endregion

        #region Serialization Support

        private List<VectorInkStroke> RecreateDryStrokes(InkModel inkDataModel)
        {
            if (inkDataModel.InkTree.Root == null)
                return new List<VectorInkStroke>();

            List<VectorInkStroke> dryStrokes = new List<VectorInkStroke>(inkDataModel.Strokes.Count);

            DecodedVectorInkBuilder decodedVectorInkBuilder = new DecodedVectorInkBuilder();

            IEnumerator<InkNode> enumerator = inkDataModel.InkTree.Root.GetRecursiveEnumerator();

            while (enumerator.MoveNext())
            {
                if (enumerator.Current is StrokeNode strokeNode)
                {
                    var vectorInkStroke = CreateDryStroke(decodedVectorInkBuilder, strokeNode.Stroke, inkDataModel);
                    dryStrokes.Add(vectorInkStroke);
                    mSpatialModel.Add(vectorInkStroke);

                    bool res = inkDataModel.SensorData.GetSensorData(vectorInkStroke.SensorDataId, out SensorData sensorData);

                    if (res)
                    {
                        mSerializer.LoadSensorDataFromModel(inkDataModel, sensorData);
                    } 
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
            Wacom.Ink.Geometry.VectorBrush vb;

            if (vectorBrush.BrushPolygons.Count > 0)
            {
                vb = new Wacom.Ink.Geometry.VectorBrush(vectorBrush.BrushPolygons.ToArray());
            }
            else if (vectorBrush.BrushPrototypeURIs.Count > 0)
            {
                List<BrushPolygon> brushPolygons = new List<BrushPolygon>(vectorBrush.BrushPrototypeURIs.Count);

                foreach (var uri in vectorBrush.BrushPrototypeURIs)
                {
                    brushPolygons.Add(new BrushPolygon(uri.MinScale, ShapeUriResolver.ResolveShape(uri.ShapeUri)));
                }

                vb = new Wacom.Ink.Geometry.VectorBrush(brushPolygons.ToArray());
            }
            else
            {
                throw new ArgumentException("Missing vector brush information! Expected BrushPolygons, BrushPolyhedrons or BrushPrototypeURIs.");
            }
            var pipelineData = decodedVectorInkBuilder.AddWholePath(stroke.Spline.ToSpline(), vb);

            return new VectorInkStroke(stroke, vb, pipelineData);
        }

        private class DecodedVectorInkBuilder
        {
            private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
            private PolygonMerger mPolygonMerger = new PolygonMerger();
            private readonly PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

            public PipelineData AddWholePath(Spline path, Wacom.Ink.Geometry.VectorBrush vectorBrush)
            {
                var splineInterpolator = new CurvatureBasedInterpolator();
                var brushApplier = new BrushApplier(vectorBrush);

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