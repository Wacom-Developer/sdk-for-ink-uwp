using System;
using System.Numerics;
using System.Collections.Generic;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    /// <summary>
    /// Base class for ink rendering brushes
    /// </summary>
    public abstract class StrokeHandler : IDisposable
    {
        [Flags]
        public enum SelectionMode 
        {
            Manipulate  = 0x01,
            Erase       = 0x02,
            Part        = 0x10,
            Whole       = 0x20
        }

        #region Fields

        protected Serializer mSerializer = new Serializer();
        protected Renderer mRenderer { get; private set; }

        #endregion

        #region Constructor

        public StrokeHandler(Renderer renderer, MediaColor color)
        {
            mRenderer = renderer;
            BrushColor = color;
        }

        #endregion

        #region Properties

        public abstract MediaColor BrushColor { get; set; }
        public MediaColor BackgroundColor { get; set; } = Colors.White;
        public abstract BrushType BrushType { get; }

        public abstract bool IsSelecting { get; }

        public abstract IEnumerable<Identifier> SelectedStrokes { get; }

        public Matrix3x2 TransformationMatrix { get; private set; } = Matrix3x2.Identity;


        //public abstract IEnumerable<object> AllStrokes { get; }

        //public abstract InkBuilder InkBuilder { get; }

        public Serializer Serializer { get => mSerializer; }

        #endregion

        #region Public Interface

        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public abstract void ClearStrokes();

        public abstract void StartSelectionMode(SelectionMode mode);
        public abstract void StopSelectionMode();


        /// <summary>
        /// Make the current stroke permanent
        /// </summary>
        public abstract void StoreCurrentStroke(PointerDeviceType deviceType);


        public abstract InkModel Serialize();

        public abstract void RenderAllStrokes(RenderingContext context, IEnumerable<Identifier> excluded, Rect? clipRect);

        /// <summary>
        /// Draw stroke
        /// </summary>
        /// <param name="renderingContext">RenderingContext to draw to</param>
        /// <param name="stroke">Cached stroke (as object)</param>
        public abstract void DoRenderStroke(RenderingContext renderingContext, object stroke, bool translationLayerPainted);

        public virtual void DrawTranslation(RenderingContext renderingContext, Layer translationLayer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles brush-specific parts of drawing a new stroke segment
        /// </summary>
        /// <param name="updateRect">returns bounding rectangle of area requiring update</param>
        public abstract void DoRenderNewStrokeSegment(out Rect updateRect);

        public virtual Rect DoRenderSelectedStrokes(RenderingContext renderingCtx, IEnumerable<Identifier> selectedStrokeIds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Loads serialized ink
        /// </summary>
        /// <param name="inkDocument"></param>
        public virtual void LoadInk(InkModel inkDocument)
        {
            // initialize serializer with loaded ink
            mSerializer.InkDocument = inkDocument;
        }

        #endregion

        #region Event Handlers

        public virtual void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            SetupStrokeTool(args.GetCurrentPoint(uiElement).PointerDevice);
        }

        public abstract void SetupStrokeTool(Windows.Devices.Input.PointerDevice device);

        /// <summary>
        /// Passes pointer event to InkBuilder to continue building ink
        /// </summary>
        /// <param name="args">Arguments returned by pointer moved event</param>
        /// <param name="uiElement">UI element associated with pointer event</param>
        public abstract void OnMoved(UIElement uiElement, PointerRoutedEventArgs args);


        /// <summary>
        /// Passes pointer event to InkBuilder to finish building ink
        /// </summary>
        /// <param name="args">Arguments returned by pointer released event</param>
        /// <param name="uiElement">UI element associated with pointer event</param>
        public abstract void OnReleased(UIElement uiElement, PointerRoutedEventArgs args);


        /// <summary>
        /// Handles any brush-specific parts of GraphicsReady event
        /// </summary>
        public abstract void DoGraphicsReady();

        public virtual void SetBrushStyle(VectorBrushStyle brushStyle)
        {
            throw new InvalidOperationException("Cannot change brush style");
        }

        public virtual void SetBrushStyle(RasterBrushStyle brushStyle)
        {
            throw new InvalidOperationException("Cannot change brush style");
        }

        #endregion

        #region IDispose support
        public abstract void Dispose();

        #endregion
    }

}