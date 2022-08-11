using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace WacomInkDemoUWP
{

    public class InkPanelView
    {
        #region Fields

        private readonly Windows.UI.Color m_backgroundColor = Colors.White;
        private readonly Windows.UI.Color m_transparentColor = Colors.Transparent;

        private bool m_redrawAllStrokes;
        private bool m_mustPresent = false;
        private readonly DirtyRectManager m_dirtyRectManager = new DirtyRectManager();

        private bool m_redrawOverlay = false;

        // WILL 3.0 native objects
        private readonly Graphics m_graphics = new Graphics();
        private Layer m_allStrokesLayer;
        private Layer m_backBufferLayer;
        private Layer m_currentStrokeLayer;
        private Layer m_prelimPathLayer;
        private Layer m_sceneLayer;
        private Layer m_translationLayer;

        private RenderingContext m_renderingContext;

        // Vector Ink
        private readonly Polygon m_addedPolygon = new Polygon();
        private readonly Polygon m_predictedPolygon = new Polygon();

        // Raster Ink
        private readonly StrokeConstants m_strokeContants = new StrokeConstants();
        private readonly ParticleList m_addedInterpolatedSpline = new ParticleList();
        private readonly ParticleList m_predictedInterpolatedSpline = new ParticleList();

        private SwapChainPanel m_swapChainPanel;
        private Size? m_newSize = null;

        private CoreIndependentInputSource m_inputSource;
        private WorkItemHandler m_mainLoopWorkItem;

        #endregion

        #region Properties

        public Matrix3x2 TransformMatrix
        {
            get
            {
                return (m_renderingContext != null) ? m_renderingContext.TransformMatrix : Matrix3x2.Identity;
            }
        }

        public Matrix3x2 InverseTransformMatrix
        {
            get
            {
                return (m_renderingContext != null) ? m_renderingContext.InverseTransformMatrix : Matrix3x2.Identity;
            }
        }

        public Graphics Graphics
        {
            get
            {
                return m_graphics;
            }
        }

        //public delegate void OnGraphicsReady(Graphics graphics);
        //public OnGraphicsReady GraphicsReady;

        #endregion

        #region Interface

        public void InitializeMainLoop(WorkItemHandler mainLoopWorkItem)
        {
            m_mainLoopWorkItem = mainLoopWorkItem;
        }

        public void Dispose()
        {
            StopProcessingEvents();
            DisposeLayers();

            Utils.SafeDispose(m_graphics);
        }

        #region Input Handling

        public void CreateInputSource(CoreInputDeviceTypes types)
        {
            m_inputSource = m_swapChainPanel.CreateCoreIndependentInputSource(types);
        }

        public void AddPointerPressedHandler(TypedEventHandler<object, PointerEventArgs> handler)
        {
            m_inputSource.PointerPressed += handler;
        }

        public void AddPointerMovedHandler(TypedEventHandler<object, PointerEventArgs> handler)
        {
            m_inputSource.PointerMoved += handler;
        }

        public void AddPointerReleasedHandler(TypedEventHandler<object, PointerEventArgs> handler)
        {
            m_inputSource.PointerReleased += handler;
        }

        public void AddPointerWheelChangedHandler(TypedEventHandler<object, PointerEventArgs> handler)
        {
            m_inputSource.PointerWheelChanged += handler;
        }

        public bool IsInputInitialized()
        {
            return m_inputSource != null;
        }

        public void ProcessEvents(CoreProcessEventsOption option)
        {
            m_inputSource.Dispatcher.ProcessEvents(option);
        }

        public void StopProcessingEvents()
        {
            m_inputSource.Dispatcher.StopProcessEvents();
        }

        public void CaptureInputPointer()
        {
            m_inputSource.SetPointerCapture();
        }

        public void ReleaseInputPointer()
        {
            m_inputSource.ReleasePointerCapture();
        }

        #endregion

        #region Graphics

        public void InitializeGraphics(SwapChainPanel swapChainPanel)
        {
            if (m_swapChainPanel != null)
                throw new InvalidOperationException("Already initialized!");

            m_swapChainPanel = swapChainPanel;
            m_swapChainPanel.SizeChanged += OnSizeChangedAsync;

            m_graphics.GraphicsReady += OnGraphicsReady;
            m_graphics.Initialize(m_swapChainPanel, true);
        }

        public bool IsGraphicsInitialized()
        {
            return m_renderingContext != null;
        }

        public void TryResize()
        {
            if (m_newSize.HasValue)
            {
                Resize();
                m_newSize = null;
            }
        }

        public void ClearLayers()
        {
            // We can currently skip this because we clear the scene layer
            // with the same color and we draw it into the backbuffer
            // with BlendMode.Copy. If there is a more complex scene,
            // this will change and these two lines will be necessary.
            //m_renderingContext.SetTarget(m_backBufferLayer);
            //m_renderingContext.ClearColor(m_backgroundColor);

            m_renderingContext.SetTarget(m_sceneLayer);
            m_renderingContext.ClearColor(m_backgroundColor);

            m_renderingContext.SetTarget(m_allStrokesLayer);
            m_renderingContext.ClearColor(m_transparentColor);

            m_renderingContext.SetTarget(m_prelimPathLayer);
            m_renderingContext.ClearColor(m_transparentColor);

            m_renderingContext.SetTarget(m_currentStrokeLayer);
            m_renderingContext.ClearColor(m_transparentColor);

            m_renderingContext.SetTarget(m_translationLayer);
            m_renderingContext.ClearColor(m_transparentColor);

        }

        public void ClearCurrentStrokeLayer()
        {
            m_renderingContext.SetTarget(m_currentStrokeLayer);
            m_renderingContext.ClearColor(m_transparentColor);

            m_dirtyRectManager.Reset();
        }

        public void DrawCurrentStrokeLayer()
        {
            m_renderingContext.SetTarget(m_allStrokesLayer);
            m_renderingContext.DrawLayer(m_currentStrokeLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver);
        }

        public void RenderNewVectorStrokeSegment(ProcessorResult<List<List<Vector2>>> rawPolygons, Color color)
        {
            PolygonUtil.ConvertPolygon(rawPolygons.Addition, m_addedPolygon);
            PolygonUtil.ConvertPolygon(rawPolygons.Prediction, m_predictedPolygon);

            // Save current transform and set identity
            var originalModelToViewMatrix = m_renderingContext.TransformMatrix;
            m_renderingContext.TransformMatrix = Matrix3x2.Identity;

            // Draw the added stroke
            m_renderingContext.SetTarget(m_currentStrokeLayer);
            Rect addedStrokeRect = m_renderingContext.FillPolygon(m_addedPolygon, color, BlendMode.Max);

            // Measure the predicted stroke
            Rect predictedStrokeRect = m_renderingContext.MeasurePolygonBounds(m_predictedPolygon);

            // Calculate the update rectangle for this frame
            Rect updateRect = m_dirtyRectManager.GetUpdateRect(addedStrokeRect, predictedStrokeRect);

            // Draw the predicted stroke
            m_renderingContext.SetTarget(m_prelimPathLayer);
            m_renderingContext.DrawLayerAtPoint(m_currentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), BlendMode.Copy);
            m_renderingContext.FillPolygon(m_predictedPolygon, color, BlendMode.Max);

            // Restore transform
            m_renderingContext.TransformMatrix = originalModelToViewMatrix;

            // Reconstruct the scene under the current stroke (only within the updated rectangle)
            ReconstructScene(updateRect);
        }

        public void RenderNewRasterStrokeSegment(ProcessorResult<Path> path, RasterDrawingTool rasterTool, Color color, ref DrawStrokeResult drawStrokeResult)
        {
            Matrix3x2 viewToModel = m_renderingContext.InverseTransformMatrix;
            float matrixScale = viewToModel.GetScale();

            Path transformedAddition = new Path(path.Addition);
            Stroke.TransformPath(transformedAddition, viewToModel, matrixScale);

            Path transformedPrediction = new Path(path.Prediction);
            Stroke.TransformPath(transformedPrediction, viewToModel, matrixScale);

            m_addedInterpolatedSpline.Assign(transformedAddition, (uint)transformedAddition.LayoutMask);
            m_predictedInterpolatedSpline.Assign(transformedPrediction, (uint)transformedPrediction.LayoutMask);

            // Set the stroke constants
            m_strokeContants.ResetToDefaultValues();
            m_strokeContants.Color = color;
            m_strokeContants.Size = rasterTool.ConstSize * matrixScale;
            m_strokeContants.Rotation = rasterTool.ConstRotation;
            m_strokeContants.ScaleX = rasterTool.Paint.ScaleX;
            m_strokeContants.ScaleY = rasterTool.Paint.ScaleY;
            m_strokeContants.OffsetX = rasterTool.Paint.OffsetX * matrixScale;
            m_strokeContants.OffsetY = rasterTool.Paint.OffsetY * matrixScale;

            ParticleBrush brush = rasterTool.RasterBrush;

            // Draw the added stroke
            m_renderingContext.SetTarget(m_currentStrokeLayer);
            drawStrokeResult = m_renderingContext.DrawParticleStroke(m_addedInterpolatedSpline, m_strokeContants, brush, BlendMode.SourceOver, drawStrokeResult.RandomGeneratorSeed);

            Rect updateRect = m_dirtyRectManager.GetUpdateRect(
                drawStrokeResult.DirtyRect,
                m_renderingContext.MeasureParticleStrokeBounds(m_predictedInterpolatedSpline, m_strokeContants, brush.Scattering));

            // Draw the predicted stroke
            m_renderingContext.SetTarget(m_prelimPathLayer);
            m_renderingContext.DrawLayerAtPoint(m_currentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), BlendMode.Copy);
            m_renderingContext.DrawParticleStroke(m_predictedInterpolatedSpline, m_strokeContants, brush, BlendMode.SourceOver, drawStrokeResult.RandomGeneratorSeed);

            // Reconstruct the scene under the current stroke (only within the updated rectangle)
            ReconstructScene(updateRect);
        }

        public void Scroll(float dx, float dy)
        {
            Matrix3x2 delta = Matrix3x2.CreateTranslation(dx, dy);

            m_renderingContext.TransformMatrix *= delta;

            TriggerRedrawSceneAndOverlay();
        }

        public void Zoom(float s)
        {
            Vector2 viewCenter = new Vector2(
                (float)(0.5 * m_backBufferLayer.Size.Width),
                (float)(0.5 * m_backBufferLayer.Size.Height));

            Matrix3x2 delta = Matrix3x2.CreateScale(s, viewCenter);

            m_renderingContext.TransformMatrix *= delta;

            TriggerRedrawSceneAndOverlay();
        }

        public void ZoomAt(Vector2 viewPoint, float s)
        {
            Matrix3x2 delta = Matrix3x2.CreateScale(s, viewPoint);

            m_renderingContext.TransformMatrix *= delta;

            TriggerRedrawSceneAndOverlay();
        }

        public void Rotate(float radians)
        {
            Vector2 viewCenter = new Vector2(
                (float)(0.5 * m_backBufferLayer.Size.Width),
                (float)(0.5 * m_backBufferLayer.Size.Height));

            Matrix3x2 delta = Matrix3x2.CreateRotation(radians, viewCenter);

            m_renderingContext.TransformMatrix *= delta;

            TriggerRedrawSceneAndOverlay();
        }

        public void ResetTransform()
        {
            m_renderingContext.TransformMatrix = Matrix3x2.Identity;

            TriggerRedrawSceneAndOverlay();
        }

        public uint WaitForSwapChain(uint milliseconds)
        {
            return m_graphics.WaitForSwapChain(milliseconds);
        }

        public bool Present()
        {
            if (m_mustPresent)
            {
                // Copy the scene to the back-buffer
                m_renderingContext.SetTarget(m_backBufferLayer);

                // Draw the translation layer after the scene
                m_renderingContext.DrawLayer(m_sceneLayer, null, Wacom.Ink.Rendering.BlendMode.Copy);
                m_renderingContext.DrawLayer(m_translationLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver); //~~~

                // Present back-buffer to the screen
                m_graphics.Present();

                m_mustPresent = false;
                return true;
            }

            return false;
        }

        public void InitialPresent()
        {
            m_renderingContext.SetTarget(m_backBufferLayer);
            m_renderingContext.DrawLayer(m_sceneLayer, null, Wacom.Ink.Rendering.BlendMode.Copy);

            // Present back-buffer to the screen
            m_graphics.Present();
        }

        public void InvalidateSceneAndOverlay()
        {
            m_redrawAllStrokes = true;
            m_redrawOverlay = true;
        }

        public void InvalidateOverlay()
        {
            m_redrawOverlay = true;
        }

        public void TriggerRedrawSceneAndOverlay()
        {
            if (!IsInputInitialized())
                return;

            InvalidateSceneAndOverlay();
            StopProcessingEvents();
        }

        public void TryRedrawAllStrokes(IEnumerable<Stroke> strokes)
        {
            if (m_redrawAllStrokes)
            {
                RedrawAllStrokes(strokes);
                m_redrawAllStrokes = false;
            }
        }

        public void TryOverlayRedraw(InkPanelModel model, Matrix3x2? translation = null)
        {
            if (m_redrawOverlay)
            {
                IEnumerable<Stroke> selStrokes = model.Strokes.Where(s => model.SelectedStrokes.Contains(s.Id));
                RedrawSelectedStrokes(selStrokes, MeasureBoundingRect(selStrokes), translation);
                m_redrawOverlay = false;
            }
        }

        public Rect MeasureBoundingRect(IEnumerable<Stroke> strokes)
        {
            Rect rcBounds = Rect.Empty;

            foreach (Stroke stroke in strokes)
            {
                Rect rcStroke = Rect.Empty;
                if (stroke is RasterStroke rasterStroke)
                {
                    LoadStrokeConstants(stroke);

                    rcStroke = m_renderingContext.MeasureParticleStrokeBounds(rasterStroke.Particles, m_strokeContants, rasterStroke.ParticleSpacing);

                }
                else if (stroke is VectorStroke vectorStroke)
                {
                    rcStroke = m_renderingContext.MeasurePolygonBounds(vectorStroke.m_polygon);
                }
                rcBounds.Union(rcStroke);
            }
            return rcBounds;
        }

        #endregion

        #endregion

        #region Implementation

        #region Input Handling

        private void StartProcessingInput()
        {
            // Run task on a dedicated high priority background thread.
            _ = ThreadPool.RunAsync(m_mainLoopWorkItem, WorkItemPriority.High, WorkItemOptions.TimeSliced);
        }

        #endregion

        #region Graphics

        private void OnGraphicsReady(Graphics sender, object o)
        {
            m_renderingContext = m_graphics.GetRenderingContext();

            //m_win2DPainter.InitWin2DObjects(m_graphics, DisplayInformation.GetForCurrentView().LogicalDpi);

            CreateLayers();
            ClearLayers();

            //m_win2DPainter.ClearWin2DLayer(m_transparentColor);

            StartProcessingInput();
        }

        private async void OnSizeChangedAsync(object sender, SizeChangedEventArgs e)
        {
            Size newSize = e.NewSize;

            await m_inputSource.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => { m_newSize = newSize; }
                );
        }

        public void ReconstructScene(Rect clipRect)
        {
            // Reconstruct the scene under the current stroke (only within the updated rectangle)
            m_renderingContext.SetTarget(m_sceneLayer, clipRect);
            m_renderingContext.ClearColor(m_backgroundColor);
            m_renderingContext.DrawLayerAtPoint(m_allStrokesLayer, clipRect, new Point(clipRect.X, clipRect.Y), Wacom.Ink.Rendering.BlendMode.SourceOver);

            // Blend the current stroke on top (only within the updated rectangle)
            m_renderingContext.DrawLayerAtPoint(m_prelimPathLayer, clipRect, new Point(clipRect.X, clipRect.Y), Wacom.Ink.Rendering.BlendMode.SourceOver);

            // Present back-buffer to the screen
            m_mustPresent = true;
        }

        private void Resize()
        {
            m_renderingContext.SetTarget(null);

            DisposeLayers();

            m_graphics.SetLogicalSize(m_newSize.Value);

            //m_win2DPainter.CreateRenderTarget();

            CreateLayers();

            InvalidateSceneAndOverlay();
        }

        //private void RedrawAllStrokesOld(IList<Stroke> strokes)
        //{
        //	if (m_fillInk)
        //	{
        //		/*	foreach (var stroke in Strokes)
        //			{
        //				// Draw current stroke
        //				m_renderingContext.SetTarget(m_currentStrokeLayer);
        //				m_renderingContext.ClearColor(m_transparentColor);
        //				m_renderingContext.FillPolygon(stroke.m_polygon, stroke.m_color, Wacom.Ink.Rendering.BlendMode.SourceOver);
        //
        //				// Blend stroke to Scene Layer
        //				m_renderingContext.SetTarget(m_sceneLayer);
        //				m_renderingContext.DrawLayer(m_currentStrokeLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver);
        //
        //				// Blend Current Stroke to All Strokes Layer
        //				m_renderingContext.SetTarget(m_allStrokesLayer);
        //				m_renderingContext.DrawLayer(m_currentStrokeLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver);
        //			}*/
        //
        //		m_renderingContext.SetTarget(m_allStrokesLayer);
        //		m_renderingContext.ClearColor(m_transparentColor);
        //
        //		foreach (var stroke in strokes)
        //		{
        //			// Draw current stroke
        //			m_renderingContext.FillPolygon(stroke.m_polygon, stroke.m_color, Wacom.Ink.Rendering.BlendMode.SourceOver);
        //		}
        //
        //		// Blend strokes to Scene Layer
        //		m_renderingContext.SetTarget(m_sceneLayer);
        //		m_renderingContext.ClearColor(m_backgroundColor);
        //		m_renderingContext.DrawLayer(m_allStrokesLayer, null, Wacom.Ink.Rendering.BlendMode.SourceOver);
        //	}
        //
        //	// Clear CurrentStroke to prepare for next draw
        //	m_renderingContext.SetTarget(m_currentStrokeLayer);
        //	m_renderingContext.ClearColor(m_transparentColor);
        //
        //	// Present back-buffer to the screen
        //	m_mustPresent = true;
        //}

        private void LoadStrokeConstants(Stroke stroke)
        {
            m_strokeContants.ResetToDefaultValues();

            m_strokeContants.Color = stroke.Color;
            m_strokeContants.Size = stroke.Size;
            m_strokeContants.Rotation = stroke.Rotation;
            m_strokeContants.ScaleX = stroke.ScaleX;
            m_strokeContants.ScaleY = stroke.ScaleY;
            m_strokeContants.OffsetX = stroke.OffsetX;
            m_strokeContants.OffsetY = stroke.OffsetY;
        }

        private void RedrawAllStrokes(IEnumerable<Stroke> strokes)
        {
            m_renderingContext.SetTarget(m_allStrokesLayer);
            m_renderingContext.ClearColor(Colors.Transparent);

            foreach (Stroke stroke in strokes)
            {
                if (stroke is RasterStroke rasterStroke)
                {
                    m_renderingContext.SetTarget(m_currentStrokeLayer);
                    m_renderingContext.ClearColor(Colors.Transparent);

                    LoadStrokeConstants(stroke);

                    m_renderingContext.DrawParticleStroke(
                        rasterStroke.Particles,
                        m_strokeContants,
                        ((AppRasterBrush)rasterStroke.Brush).ParticleBrush,
                        rasterStroke.Brush.BlendMode,
                        rasterStroke.RandomSeed);

                    // Blend Current Stroke to All Strokes Layer
                    m_renderingContext.SetTarget(m_allStrokesLayer);
                    m_renderingContext.DrawLayer(m_currentStrokeLayer, null, BlendMode.SourceOver);     // FIX: copy just the stroke rect?
                }
                else if (stroke is VectorStroke vectorStroke)
                {
                    if (vectorStroke.BlendMode == BlendMode.SourceOver)
                    {
                        m_renderingContext.SetTarget(m_allStrokesLayer);
                        m_renderingContext.FillPolygon(vectorStroke.m_polygon, vectorStroke.Color, BlendMode.SourceOver);
                    }
                    else
                    {
                        // Draw stroke to a transparent layer
                        m_renderingContext.SetTarget(m_currentStrokeLayer);
                        m_renderingContext.ClearColor(Colors.Transparent);
                        m_renderingContext.FillPolygon(vectorStroke.m_polygon, vectorStroke.Color, BlendMode.SourceOver);

                        // Blend Current Stroke to All Strokes Layer
                        m_renderingContext.SetTarget(m_allStrokesLayer);
                        m_renderingContext.DrawLayer(m_currentStrokeLayer, null, BlendMode.SourceOver);
                    }
                }
            }

            // Blend stroke to Scene Layer
            m_renderingContext.SetTarget(m_sceneLayer);
            m_renderingContext.ClearColor(m_backgroundColor);
            m_renderingContext.DrawLayer(m_allStrokesLayer, null, BlendMode.SourceOver);

            // Clear CurrentStroke to prepare for future drawing
            m_renderingContext.SetTarget(m_currentStrokeLayer);
            m_renderingContext.ClearColor(Colors.Transparent);

            // Present back buffer to the screen
            m_mustPresent = true;
        }


        public void RedrawSelectedStrokes(IEnumerable<Stroke> strokes, Rect rcBounds, Matrix3x2? translation)
        {
            m_renderingContext.SetTarget(m_translationLayer);
            m_renderingContext.ClearColor(Colors.Transparent);      // Clear whole layer

            if (translation.HasValue)
            {
                m_renderingContext.TransformMatrix = translation.Value;

                Rect rcOffset = rcBounds;
                rcOffset.X += translation.Value.Translation.X;
                rcOffset.Y += translation.Value.Translation.Y;
                m_renderingContext.SetClipRect(rcOffset);
            }
            else
            {
                m_renderingContext.SetClipRect(rcBounds);
            }

            m_renderingContext.ClearColor(Color.FromArgb(27, 0, 0, 0)); // Clear bounding rect to back semi-transparent 

            foreach (Stroke stroke in strokes)
            {
                if (stroke is RasterStroke rasterStroke)
                {
                    LoadStrokeConstants(stroke);

                    AppRasterBrush rasterBrush = stroke.Brush as AppRasterBrush;
                    m_renderingContext.DrawParticleStroke(
                        rasterStroke.Particles,
                        m_strokeContants,
                        rasterBrush.ParticleBrush,
                        rasterStroke.Brush.BlendMode,
                        rasterStroke.RandomSeed);
                }
                else if (stroke is VectorStroke vectorStroke)
                {
                    m_renderingContext.FillPolygon(vectorStroke.m_polygon, vectorStroke.Color, BlendMode.SourceOver);
                }
            }

            m_renderingContext.TransformMatrix = Matrix3x2.Identity;

            // Present back buffer to the screen
            m_mustPresent = true;
        }

        private void CreateLayers()
        {
            Size size = m_graphics.Size;
            float scale = m_graphics.Scale;

            m_allStrokesLayer = m_graphics.CreateLayer(size, scale);
            m_backBufferLayer = m_graphics.CreateBackbufferLayer();
            m_currentStrokeLayer = m_graphics.CreateLayer(size, scale);
            m_prelimPathLayer = m_graphics.CreateLayer(size, scale);
            m_sceneLayer = m_graphics.CreateLayer(size, scale);
            m_translationLayer = Graphics.CreateLayer(size, scale);
            //m_win2DGraphicsLayer = m_win2DPainter.CreateLayer();
        }

        private void DisposeLayers()
        {
            Utils.SafeDispose(m_allStrokesLayer);
            Utils.SafeDispose(m_backBufferLayer);
            Utils.SafeDispose(m_currentStrokeLayer);
            Utils.SafeDispose(m_prelimPathLayer);
            Utils.SafeDispose(m_sceneLayer);
            Utils.SafeDispose(m_translationLayer);
            //Utils.SafeDispose(m_win2DGraphicsLayer);
        }

        #endregion

        #endregion
    }
}
