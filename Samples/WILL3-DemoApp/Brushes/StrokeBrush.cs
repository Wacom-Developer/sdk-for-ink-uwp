//using DigitalInk;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

using Wacom.Ink.Rendering;
//using Windows.Foundation;
//using Windows.UI;
//using Windows.UI.Xaml;
//using Windows.UI.Xaml.Input;

namespace Wacom
{
    /// <summary>
    /// Base class for ink rendering brushes
    /// </summary>
    public abstract class StrokeBrush : IDisposable
    {
        #region Constructor

        public StrokeBrush(Color color)
        {
            BrushColor = color;
        }

        #endregion

        #region Properties

        public Color BrushColor { get; set; }
        public Color BackgroundColor { get; set; } = Colors.White;
        public abstract Type BrushType { get; }

        protected StrokeConstants StrokeParams { get; set; }

        /// <summary>
        /// InkBuilder (Vector or Raster) handling pipeline stages for building ink
        /// </summary>
        public abstract InkBuilder InkBuilder { get; }

        public abstract IEnumerable<object> AllStrokes { get; }

        #endregion


#if false
        #region Public Interface

        /// <summary>
        /// Change attributes of the brush
        /// </summary>
        /// <param name="thickness">new brush thickness</param>
        /// <param name="color">new brush color</param>
        /// <param name="shape">new brush shape (ignored by RasterBrush)</param>
        public abstract void Change(Thickness thickness, Color color, Shape shape);


        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public abstract void ClearStrokes();


        /// <summary>
        /// Make the current stroke permanent
        /// </summary>
        public abstract void StoreCurrentStroke();


        /// <summary>
        /// Draw stroke
        /// </summary>
        /// <param name="renderingContext">RenderingContext to draw to</param>
        /// <param name="stroke">Cached stroke (as object)</param>
        public abstract void DoRenderStroke(RenderingContext renderingContext, object stroke);


        /// <summary>
        /// Handles brush-specific parts of drawing a new stroke segment
        /// </summary>
        /// <param name="renderer">Renderer object containing RenderingContext and Layers for drawing</param>
        /// <param name="updateRect">returns bounding rectangle of area requiring update</param>
        public abstract void DoRenderNewStrokeSegment(Renderer renderer, out Rect updateRect);

        #endregion

        #region Event Handlers

        /// <summary>
        /// Passes pointer event to InkBuilder to begin building ink
        /// </summary>
        /// <param name="args">Arguments returned by pointer pressed event</param>
        /// <param name="uiElement">UI element associated with pointer event</param>
        public virtual void DoPointerPressed(PointerRoutedEventArgs args, UIElement uiElement)
        {
            InkBuilder.AddPointsFromEvent(Phase.Begin, uiElement, args);
        }


        /// <summary>
        /// Passes pointer event to InkBuilder to continue building ink
        /// </summary>
        /// <param name="args">Arguments returned by pointer moved event</param>
        /// <param name="uiElement">UI element associated with pointer event</param>
        public virtual void DoPointerMoved(PointerRoutedEventArgs args, UIElement uiElement)
        {
            InkBuilder.AddPointsFromEvent(Phase.Update, uiElement, args);
        }


        /// <summary>
        /// Passes pointer event to InkBuilder to finish building ink
        /// </summary>
        /// <param name="args">Arguments returned by pointer released event</param>
        /// <param name="uiElement">UI element associated with pointer event</param>
        public virtual void DoPointerReleased(PointerRoutedEventArgs args, UIElement uiElement)
        {
            InkBuilder.AddPointsFromEvent(Phase.End, uiElement, args);
        }


        /// <summary>
        /// Handles any brush-specific parts of GraphicsReady event
        /// </summary>
        public abstract void DoGraphicsReady();

        #endregion


#endif        
        
        #region IDispose support
        public abstract void Dispose();

        #endregion
    }

}