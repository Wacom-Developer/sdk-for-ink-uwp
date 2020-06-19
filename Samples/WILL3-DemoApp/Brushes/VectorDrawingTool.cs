using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Geometry;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

    /// <summary>
    /// Abstract base class for raster based drawing tools 
    /// </summary>
    public abstract class VectorDrawingTool : DrawingTool
    {
        protected static readonly VectorBrush mCircleBrush = new VectorBrush(
            new BrushPolygon(0.0f, VectorBrushFactory.CreateEllipseBrush(4, 1.0f, 1.0f)),
            new BrushPolygon(2.0f, VectorBrushFactory.CreateEllipseBrush(8, 1.0f, 1.0f)),
            new BrushPolygon(6.0f, VectorBrushFactory.CreateEllipseBrush(16, 1.0f, 1.0f)),
            new BrushPolygon(18.0f, VectorBrushFactory.CreateEllipseBrush(32, 1.0f, 1.0f)));

        public VectorInkBuilder InkBuilder { get; } = new VectorInkBuilder();

        public abstract VectorBrush Shape { get; }

        public ProcessorResult<List<PolygonVertices>> Polygons { get; private set; }

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Begin, uiElement, args);
            Polygons = InkBuilder.GetPolygons();
            PointsAdded?.Invoke(this, null);
        }

        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Update, uiElement, args);
            Polygons = InkBuilder.GetPolygons();
            PointsAdded?.Invoke(this, null);
        }
        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.End, uiElement, args);
            Polygons = InkBuilder.GetPolygons();
            DrawingFinished?.Invoke(this, BlendCurrentStroke);
        }

    }

    /// <summary>
    /// Vector drawing tool for rendering pen-style output
    /// </summary>
    class PenTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 5,
            maxSpeed = 210,
            minValue = 0.5f,
            maxValue = 1.6f,
            remap = v => (1 + 0.62f) * v / ((float)Math.Abs(v) + 0.62f)
        };

        public override VectorBrush Shape => mCircleBrush; 
        protected override ToolConfig SizeConfig => mConfig; 
    }

    /// <summary>
    /// Vector drawing tool for rendering felt pen-style output
    /// </summary>
    class FeltTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 33,
            maxSpeed = 628,
            minValue = 1.03f,
            maxValue = 2.43f,
            remap = v => 0.5f - 0.5f * (float)Math.Cos(3 * Math.PI * v)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig; 
    }

    /// <summary>
    /// Vector drawing tool for rendering brush-style output
    /// </summary>
    class BrushTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 182,
            maxSpeed = 3547,
            minValue = 3.4f,
            maxValue = 17.2f,
            remap = v => (float)Math.Pow(v, 1.19),
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig; 
        protected override float? Alpha => 0.7f;
    }

    /// <summary>
    /// Vector drawing tool for rendering pen-style output
    /// </summary>
    class VectorSelectionTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 5,
            maxSpeed = 210,
            minValue = 0.5f,
            maxValue = 1.6f,
            remap = v => (1 + 0.62f) * v / ((float)Math.Abs(v) + 0.62f)
        };

        private Point m_startPosition;
        private Point m_prevPosition;
        private bool m_isTranslating = false;
        private VectorStrokeHandler m_strokeHandler;

        public VectorSelectionTool(VectorStrokeHandler strokeHandler)
        {
            m_strokeHandler = strokeHandler;
        }

        public override bool BlendCurrentStroke => false;

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;

        public List<Identifier> SelectedStrokes { get; } = new List<Identifier>();
        public Rect DestRect { get; set; } = Rect.Empty;
        public Rect SourceRect { get; set; } = Rect.Empty;

        public event EventHandler OnTranslate;
        public event EventHandler<Matrix3x2> TranslateFinished;

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            var currentPt = args.GetCurrentPoint(uiElement).Position;
            m_isTranslating = CurrentPointIntersectsSourceRect(currentPt);

            if (m_isTranslating)
            {
                m_startPosition = currentPt;
                m_prevPosition = m_startPosition;
                OnTranslate?.Invoke(this, null);
            }
            else
            {
                base.OnPressed(uiElement, args);
            }
        }

        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            if (m_isTranslating)
            {
                var pt = args.GetCurrentPoint(uiElement).Position;

                double dX = pt.X - m_prevPosition.X;
                double dY = pt.Y - m_prevPosition.Y;
                Matrix3x2 translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);

                m_prevPosition = pt;

                Vector2 newPosition = Vector2.Transform(new Vector2((float)DestRect.X, (float)DestRect.Y), translation);
                DestRect = new Rect(newPosition.X, newPosition.Y, DestRect.Width, DestRect.Height);
                OnTranslate?.Invoke(this, null);
            }
            else
            {
                base.OnMoved(uiElement, args);
            }
        }

        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            if (m_isTranslating)
            {
                //m_isTranslating = false;

                Matrix3x2 translation = Matrix3x2.Identity;

                var pt = args.GetCurrentPoint(uiElement).Position;
                if (!m_strokeHandler.TransformationMatrix.IsIdentity)
                {
                    bool res = Matrix3x2.Invert(m_strokeHandler.TransformationMatrix, out Matrix3x2 modelTransformationMatrix);

                    if (!res)
                    {
                        throw new InvalidOperationException("Transform matrix could not be inverted.");
                    }

                    Vector2 currentPointPos = new Vector2((float)pt.X, (float)pt.Y);
                    Vector2 startPos = new Vector2((float)m_startPosition.X, (float)m_startPosition.Y);
                    Vector2 modelCurrentPointPos = Vector2.Transform(currentPointPos, modelTransformationMatrix);
                    Vector2 modelStartPos = Vector2.Transform(startPos, modelTransformationMatrix);

                    double dX = modelCurrentPointPos.X - modelStartPos.X;
                    double dY = modelCurrentPointPos.Y - modelStartPos.Y;
                    translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);
                }
                else
                {
                    double dX = pt.X - m_startPosition.X;
                    double dY = pt.Y - m_startPosition.Y;
                    translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);
                }

                TranslateFinished?.Invoke(this, translation);
            }
            else
            {
                base.OnReleased(uiElement, args);
            }

            m_isTranslating = false;
        }

        private bool CurrentPointIntersectsSourceRect(Point currentPoint)
        {
            if (SourceRect.IsEmpty)
                return false;

            return SourceRect.Contains(currentPoint);
        }
    }
    
}
