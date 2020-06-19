using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;


using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulation;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    /// <summary>
    /// Creates and manages rendering brush and graphics objects and interactions between them;
    /// Recieves pointer input events and passes them on to the rendering brush to feed into 
    /// the ink geometry pipeline
    /// </summary>
    public class Renderer
    {

        #region Fields

        private SwapChainPanel m_swapChainPanel;
        private PointerManager m_pointerManager;
        private Graphics m_graphics = new Graphics();

        #endregion

        #region Properties

        public StrokeHandler StrokeHandler { get; private set; }

        public Graphics Graphics => m_graphics;

        public DirtyRectManager DirtyRectManager { get; private set; } = new DirtyRectManager();

        public RenderingContext RenderingContext { get; private set; }
        public Layer BackBufferLayer { get; private set; }
        public Layer SceneLayer { get; private set; }
        public Layer AllStrokesLayer { get; private set; }
        public Layer PrelimPathLayer { get; private set; }
        public Layer CurrentStrokeLayer { get; private set; }

        public Layer TranslationLayer { get; private set; }

        public bool TranslationLayerPainted { get; private set; } = false;

        #endregion

        #region Constructor


        /// <summary>
        /// Constructor. Creates rendering brush; initializes graphics
        /// </summary>
        /// <param name="swapChainPanel">SwapChainPanel on which to render captured ink</param>
        /// <param name="brushType">Type of brush to use</param>
        /// <param name="thickness">Relative thickness of brush</param>
        /// <param name="color">Color of ink</param>
        /// <param name="style">Shape of brush (VectorBrush only)</param>
        public Renderer(SwapChainPanel swapChainPanel, VectorBrushStyle style, MediaColor color)
        {
            StrokeHandler = new VectorStrokeHandler(this, style, color);

            m_swapChainPanel = swapChainPanel;
            m_graphics.GraphicsReady += OnGraphicsReady;
            m_graphics.Initialize(m_swapChainPanel, false);
        }

        public Renderer(SwapChainPanel swapChainPanel, RasterBrushStyle style, MediaColor color)
        {
            StrokeHandler = new RasterStrokeHandler(this, style, color, m_graphics);

            m_swapChainPanel = swapChainPanel;
            m_graphics.GraphicsReady += OnGraphicsReady;
            m_graphics.Initialize(m_swapChainPanel, false);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Registers handlers for SwapChainPanel events
        /// </summary>
        public void StartProcessingInput()
        {
            m_swapChainPanel.PointerPressed += OnPointerPressed;
            m_swapChainPanel.PointerMoved += OnPointerMoved;
            m_swapChainPanel.PointerReleased += OnPointerReleased;
            m_swapChainPanel.SizeChanged += OnSizeChanged;
        }

        /// <summary>
        /// Unregisters handlers for SwapChainPanel events
        /// </summary>
        public void StopProcessingInput()
        {
            m_swapChainPanel.PointerPressed -= OnPointerPressed;
            m_swapChainPanel.PointerMoved -= OnPointerMoved;
            m_swapChainPanel.PointerReleased -= OnPointerReleased;
            m_swapChainPanel.SizeChanged -= OnSizeChanged;
        }

        /// <summary>
        /// Event handler for Graphics object completing initialization
        /// </summary>
        public virtual void OnGraphicsReady(Graphics sender, object o)
        {
            StrokeHandler.DoGraphicsReady();

            RenderingContext = m_graphics.GetRenderingContext();

            CreateLayers();
            ClearLayers();

            StartProcessingInput();
            PresentGraphics();
        }

        /// <summary>
        /// Initiates capture of an ink stroke
        /// </summary>
        private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (!m_pointerManager.OnPressed(args))
                return;

            m_swapChainPanel.CapturePointer(args.Pointer);

            StrokeHandler.OnPressed(m_swapChainPanel, args);
        }

        /// <summary>
        /// Updates current ink stroke with new pointer input
        /// </summary>
        private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
        {
            // Ignore events from other pointers
            if (!m_pointerManager.OnMoved(args))
                return;

            StrokeHandler.OnMoved(m_swapChainPanel, args);
        }

        /// <summary>
        /// Completes capture of an ink stroke
        /// </summary>
        private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
        {
            // Ignore events from other pointers
            if (!m_pointerManager.OnReleased(args))
                return;

            m_swapChainPanel.ReleasePointerCapture(args.Pointer);

            StrokeHandler.OnReleased(m_swapChainPanel, args);

            if (!StrokeHandler.IsSelecting)
            {
                RenderNewStrokeSegment();

                BlendCurrentStrokesLayerToAllStrokesLayer();

                RenderBackbuffer();
                PresentGraphics();

                var ptrDevice = args.GetCurrentPoint(m_swapChainPanel).PointerDevice;
                StrokeHandler.StoreCurrentStroke(ptrDevice.PointerDeviceType);
            }
        }

        private void BlendCurrentStrokesLayerToAllStrokesLayer()
        {
            RenderingContext.SetTarget(AllStrokesLayer);
            RenderingContext.DrawLayer(CurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

            RenderingContext.SetTarget(CurrentStrokeLayer);
            RenderingContext.ClearColor(Colors.Transparent);

            DirtyRectManager.Reset();
        }

        /// <summary>
        /// Event handler for change of size of SwapChainPanel 
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Reinitialize layers
            RenderingContext.SetTarget(null);

            DisposeLayers();

            m_graphics.SetLogicalSize(e.NewSize);

            CreateLayers();
            ClearLayers();

            // Redraw saved strokes
            if (StrokeHandler.IsSelecting)
            {
                RenderSelectedStrokes(StrokeHandler.SelectedStrokes);
                RedrawAllStrokes(StrokeHandler.SelectedStrokes, null);

            }
            else
            {
                RedrawAllStrokes(null, null);
            }
        }

        #endregion

        public void RenderBackbuffer()
        {
            // Copy the scene to the backbuffer
            RenderingContext.SetTarget(BackBufferLayer);

            RenderingContext.DrawLayer(SceneLayer, null, Wacom.Ink.Rendering.BlendMode.Copy);

            if (StrokeHandler.IsSelecting && TranslationLayerPainted)
            {
                StrokeHandler.DrawTranslation(RenderingContext, TranslationLayer);
            }
        }

        #region Stroke Handling

        public void SetHandler(VectorBrushStyle brushStyle, MediaColor brushColor)
        {
            if (StrokeHandler is VectorStrokeHandler)
            {
                StrokeHandler.SetBrushStyle(brushStyle);
                StrokeHandler.BrushColor = brushColor;
            }
            else
            {
                StrokeHandler = new VectorStrokeHandler(this, brushStyle, brushColor);
                // Clear screen
                ClearLayers();
                PresentGraphics();
            }
        }

        public void SetHandler(RasterBrushStyle brushStyle, MediaColor brushColor)
        {
            if (StrokeHandler is RasterStrokeHandler)
            {
                StrokeHandler.SetBrushStyle(brushStyle);
                StrokeHandler.BrushColor = brushColor;
            }
            else
            {
                StrokeHandler = new RasterStrokeHandler(this, brushStyle, brushColor, m_graphics);
                StrokeHandler.DoGraphicsReady();
                ClearLayers();
                PresentGraphics();
            }
        }

        public void PresentGraphics()
        {
            m_graphics.Present();
        }
       
        public void InvokeRenderSelected(List<Identifier> selectedStrokes)
        {
            var ignored = m_swapChainPanel.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                ClearLayers();
                RenderSelectedStrokes(selectedStrokes);
                RedrawAllStrokes(selectedStrokes, null);
            });
        }

        /// <summary>
        /// Discard all captured ink
        /// </summary>        
        public void ClearStrokes()
        {
            bool redrawAll = StrokeHandler.IsSelecting;
            
            // Delete saved strokes
            StrokeHandler.ClearStrokes();

            // Clear screen
            ClearLayers();
            if (redrawAll)
            {
                RedrawAllStrokes(null, null);
                RenderBackbuffer();
            }
            PresentGraphics();
        }

        /// <summary>
        /// Update display with newly captured ink
        /// </summary>
        public virtual void RenderNewStrokeSegment()
        {
            // Do brush-specific parts of rendering
            StrokeHandler.DoRenderNewStrokeSegment(out Rect updateRect);

            // Reconstruct the scene under the current stroke (only within the updated rect)
            ReconstructScene(updateRect);
        }

        private void ReconstructScene(Rect clipRect)
        {
            // Reconstruct the scene under the current stroke (only within the updated rect)
            RenderingContext.SetTarget(SceneLayer, clipRect);
            RenderingContext.ClearColor(StrokeHandler.BackgroundColor);
            RenderingContext.DrawLayerAtPoint(AllStrokesLayer, clipRect, new Point(clipRect.X, clipRect.Y), Ink.Rendering.BlendMode.SourceOver);

            // Blend the current stroke on top (only within the updated rect)
            RenderingContext.DrawLayerAtPoint(PrelimPathLayer, clipRect, new Point(clipRect.X, clipRect.Y), Ink.Rendering.BlendMode.SourceOver);
        }

        /// <summary>
        /// Redraw all saved strokes
        /// </summary>
        /// <param name="clipRect">Optional clipping rectangle</param>
        public void RedrawAllStrokes(IEnumerable<Identifier> excluded, Rect? clipRect)
        {
            RenderingContext.SetTarget(AllStrokesLayer, clipRect);
            RenderingContext.ClearColor(Colors.Transparent);

            StrokeHandler.RenderAllStrokes(RenderingContext, excluded, clipRect);

            // Clear CurrentStroke to prepare for next draw
            RenderingContext.SetTarget(CurrentStrokeLayer);
            RenderingContext.ClearColor(Colors.Transparent);

            RenderBackbuffer();

            // Present backbuffer to the screen
            PresentGraphics();
        }

        public void RenderSelectedStrokes(IEnumerable<Identifier> selectedStrokeIds)
        {
            if (!StrokeHandler.IsSelecting)
            {
                throw new InvalidOperationException("Unexpected call to RenderSelectedStrokes");
            }

            RenderingContext.SetTarget(TranslationLayer);
            RenderingContext.ClearColor(MediaColor.FromArgb(27, 0, 0, 0)); // Black transparent background

            RenderingContext.TransformMatrix = StrokeHandler.TransformationMatrix;

            // Draw the selected strokes
            Rect rect = StrokeHandler.DoRenderSelectedStrokes(RenderingContext, selectedStrokeIds);

            if (selectedStrokeIds.Count() == 0)
            {
                TranslationLayerPainted = false;
            }
            else
            {
                TranslationLayerPainted = true;
            }
            RenderingContext.TransformMatrix = Matrix3x2.Identity;
        }


        /// <summary>
        /// Loads serialized ink
        /// </summary>
        /// <param name="inkDocument"></param>
        public virtual void LoadInk(InkModel inkDocument)
        {
            StrokeHandler.LoadInk(inkDocument);
        }

        #endregion

        #region Selection

        public bool IsSelecting => StrokeHandler.IsSelecting;

        public void StartSelectionMode()
        {
            StrokeHandler.StartSelectionMode();
        }
        public void StopSelectionMode()
        {
            StrokeHandler.StopSelectionMode();
        }
        #endregion

        #region Layer Support

        /// <summary>
        /// Creates layers
        /// </summary>
        public void CreateLayers()
        {
            Size size = m_graphics.Size;
            float scale = m_graphics.Scale;

            BackBufferLayer = m_graphics.CreateBackbufferLayer();
            SceneLayer = m_graphics.CreateLayer(size, scale);
            AllStrokesLayer = m_graphics.CreateLayer(size, scale);
            PrelimPathLayer = m_graphics.CreateLayer(size, scale);
            CurrentStrokeLayer = m_graphics.CreateLayer(size, scale);
            TranslationLayer = m_graphics.CreateLayer(size, scale);
        }

        /// <summary>
        /// Clears all layers
        /// </summary>
        public void ClearLayers()
        {
            RenderingContext.SetTarget(BackBufferLayer);
            RenderingContext.ClearColor(StrokeHandler.BackgroundColor);

            RenderingContext.SetTarget(SceneLayer);
            RenderingContext.ClearColor(StrokeHandler.BackgroundColor);

            RenderingContext.SetTarget(AllStrokesLayer);
            RenderingContext.ClearColor(Colors.Transparent);

            RenderingContext.SetTarget(PrelimPathLayer);
            RenderingContext.ClearColor(Colors.Transparent);

            RenderingContext.SetTarget(CurrentStrokeLayer);
            RenderingContext.ClearColor(Colors.Transparent);

            RenderingContext.SetTarget(TranslationLayer);
            RenderingContext.ClearColor(Colors.Transparent);
        }

        /// <summary>
        /// Safely disposes of all layers
        /// </summary>
        public void DisposeLayers()
        {
            Utils.SafeDispose(BackBufferLayer);
            Utils.SafeDispose(SceneLayer);
            Utils.SafeDispose(AllStrokesLayer);
            Utils.SafeDispose(PrelimPathLayer);
            Utils.SafeDispose(CurrentStrokeLayer);
            Utils.SafeDispose(TranslationLayer);
        }

        #endregion
        
        #region IDispose support

        public void Dispose()
        {
            StopProcessingInput();

            DisposeLayers();

            StrokeHandler.Dispose();

            Utils.SafeDispose(m_graphics);
        }

        #endregion
    }
}
