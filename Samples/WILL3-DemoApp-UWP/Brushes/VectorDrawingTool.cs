using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulation;

namespace Wacom
{
    using Windows.Devices.Input;
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

        public VectorInkBuilder InkBuilder { get; } = new VectorInkBuilder(true);

        public abstract VectorBrush Shape { get; }

        public ProcessorResult<List<PolygonVertices>> Polygons { get; private set; }

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Begin, uiElement, args);
            Polygons = InkBuilder.GetCurrentPolygons();
            PointsAdded?.Invoke(this, null);
        }

        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Update, uiElement, args);
            Polygons = InkBuilder.GetCurrentPolygons();
            PointsAdded?.Invoke(this, null);
        }
        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.End, uiElement, args);
            Polygons = InkBuilder.GetCurrentPolygons();
            DrawingFinished?.Invoke(this, BlendCurrentStroke);
        }

        /// <summary>
        /// Calculator delegate for input from mouse input
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected PathPoint CalculatorForMouseAndTouch(PointerData previous, PointerData current, PointerData next)
        {
            var size = current.ComputeValueBasedOnSpeed(previous, next, SizeConfig.minValue, SizeConfig.maxValue, SizeConfig.initValue, SizeConfig.finalValue, SizeConfig.minSpeed, SizeConfig.maxSpeed, SizeConfig.remap);

            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size
            };

            return pp;
        }
    }

    /// <summary>
    /// Vector drawing tool for rendering pen-style output
    /// </summary>
    class PenTool : VectorDrawingTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 180,
            maxSpeed = 2100,
            minValue = 1.5f,
            maxValue = 3,
            initValue = 1.5f,
            finalValue = 1.5f,
            remap = v => (float)Math.Pow(v, 0.35f)
        };

        protected override float PreviousSize { get; set; } = 1.5f;

        public override LayoutMask GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        public override Calculator GetCalculator(PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return CalculatorForMouseAndTouch;
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForStylus;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            if (!current.Force.HasValue)
            {
                return CalculatorForMouseAndTouch(previous, current, next);
            }
            else
            {
                var size = ComputeValueBasedOnPressure(current, 1.5f, 3f, 180f, 2100f, false, v => (float)Math.Pow(v, 0.35f));

                if (size.HasValue)
                {
                    PreviousSize = size.Value;
                }
                else
                {
                    size = PreviousSize;
                }

                PathPoint pp = new PathPoint(current.X, current.Y)
                {
                    Size = size
                };
                return pp;
            }
        }

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
            minSpeed = 80,
            maxSpeed = 1400,
            minValue = 3,
            maxValue = 7,
            initValue = 3f,
            finalValue = 3f,
            remap = v => (float)Math.Pow(v, 0.65f)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;

        protected override float PreviousSize { get; set; } = 2f;

        public override LayoutMask GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size |
                            LayoutMask.Rotation |
                            LayoutMask.ScaleX |
                            LayoutMask.OffsetX;

                default:
                    throw new Exception("Unknown input device type");
            }
        }

        public override Calculator GetCalculator(PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return CalculatorForMouseAndTouch;
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForStylus;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            float? size;

            if (!current.Force.HasValue)
            {
                size = current.ComputeValueBasedOnSpeed(previous, next, 1, 5, null, null, 0, 3500, v => (float)Math.Pow(v, 1.17f));
            }
            else
            {
                size = ComputeValueBasedOnPressure(current, 1, 5, 0, 1, false, v => (float)Math.Pow(v, 1.17f));
            }
            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }


            var cosAltitudeAngle = (float)Math.Abs(Math.Cos(current.AltitudeAngle.Value));

            var tiltScale = 1.5f * cosAltitudeAngle;
            var scaleX = 1.0f + tiltScale;
            var offsetX = size * tiltScale;
            var rotation = current.ComputeNearestAzimuthAngle(previous);

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = rotation,
                ScaleX = scaleX,
                OffsetX = offsetX
            };
            return pp;
        }
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
            minValue = 10,
            maxValue = 17.2f,
            initValue = 10f,
            finalValue = 10f,
            remap = v => (float)Math.Pow(v, 1.19f)
        };

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;
        protected override float? Alpha => 0.7f;

        protected override float PreviousSize { get; set; } = 10;


        public override LayoutMask GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size |
                            LayoutMask.Rotation |
                            LayoutMask.ScaleX |
                            LayoutMask.OffsetX;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        public override Calculator GetCalculator(PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return CalculatorForMouseAndTouch;
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForStylus;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            float? size;

            if (!current.Force.HasValue)
            {
                size = current.ComputeValueBasedOnSpeed(previous, next, 1.5f, 10.2f, null, null, 0, 3500, v => (float)Math.Pow(v, 1.17f));
            }
            else
            {
                size = ComputeValueBasedOnPressure(current, 1.5f, 10.2f, 0, 1, false, v => (float)Math.Pow(v, 1.17f));
            }
            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }


            var cosAltitudeAngle = (float)Math.Abs(Math.Cos(current.AltitudeAngle.Value));

            var tiltScale = 1.5f * cosAltitudeAngle;
            var scaleX = 1.0f + tiltScale;
            var offsetX = size * tiltScale;
            var rotation = current.ComputeNearestAzimuthAngle(previous);

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = rotation,
                ScaleX = scaleX,
                OffsetX = offsetX
            };
            return pp;
        }

    }

    public abstract class VectorSelectionTool : VectorDrawingTool
    {
        public ManipulationMode ManipulationMode { get; private set; }

        protected VectorSelectionTool(ManipulationMode mode)
        {
            ManipulationMode = mode;
        }

        public List<Identifier> SelectedStrokes { get; } = new List<Identifier>();


    }

    /// <summary>
    /// Vector "drawing" tool for selecting ink storkes
    /// </summary>
    public class VectorManipulationTool : VectorSelectionTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            minSpeed = 5,
            maxSpeed = 210,
            minValue = 0.5f,
            maxValue = 1.6f,
            remap = v => (1 + 0.62f) * v / ((float)Math.Abs(v) + 0.62f)
        };

        private Point mStartPosition;
        private Point mPrevPosition;
        private bool mIsTranslating = false;
        private VectorStrokeHandler mStrokeHandler;

        public VectorManipulationTool(VectorStrokeHandler strokeHandler, ManipulationMode mode)
            : base(mode)
        {
            mStrokeHandler = strokeHandler;
        }

        public override bool BlendCurrentStroke => false;

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;

        protected override float PreviousSize { get; set; } = 1.5f;

        public Rect DestRect { get; set; } = Rect.Empty;
        public Rect SourceRect { get; set; } = Rect.Empty;

        public event EventHandler OnTranslate;
        public event EventHandler<Matrix3x2> TranslateFinished;

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            var currentPt = args.GetCurrentPoint(uiElement).Position;
            mIsTranslating = CurrentPointIntersectsSourceRect(currentPt);

            if (mIsTranslating)
            {
                mStartPosition = currentPt;
                mPrevPosition = mStartPosition;
                OnTranslate?.Invoke(this, null);
            }
            else
            {
                base.OnPressed(uiElement, args);
            }
        }

        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            if (mIsTranslating)
            {
                var pt = args.GetCurrentPoint(uiElement).Position;

                double dX = pt.X - mPrevPosition.X;
                double dY = pt.Y - mPrevPosition.Y;
                Matrix3x2 translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);

                mPrevPosition = pt;

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
            if (mIsTranslating)
            {
                Matrix3x2 translation = Matrix3x2.Identity;

                var pt = args.GetCurrentPoint(uiElement).Position;
                if (!mStrokeHandler.TransformationMatrix.IsIdentity)
                {
                    bool res = Matrix3x2.Invert(mStrokeHandler.TransformationMatrix, out Matrix3x2 modelTransformationMatrix);

                    if (!res)
                    {
                        throw new InvalidOperationException("Transform matrix could not be inverted.");
                    }

                    Vector2 currentPointPos = new Vector2((float)pt.X, (float)pt.Y);
                    Vector2 startPos = new Vector2((float)mStartPosition.X, (float)mStartPosition.Y);
                    Vector2 modelCurrentPointPos = Vector2.Transform(currentPointPos, modelTransformationMatrix);
                    Vector2 modelStartPos = Vector2.Transform(startPos, modelTransformationMatrix);

                    double dX = modelCurrentPointPos.X - modelStartPos.X;
                    double dY = modelCurrentPointPos.Y - modelStartPos.Y;
                    translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);
                }
                else
                {
                    double dX = pt.X - mStartPosition.X;
                    double dY = pt.Y - mStartPosition.Y;
                    translation = Matrix3x2.CreateTranslation((float)dX, (float)dY);
                }

                TranslateFinished?.Invoke(this, translation);
            }
            else
            {
                base.OnReleased(uiElement, args);
            }

            mIsTranslating = false;
        }

        private bool CurrentPointIntersectsSourceRect(Point currentPoint)
        {
            if (SourceRect.IsEmpty)
                return false;

            return SourceRect.Contains(currentPoint);
        }

        public override LayoutMask GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        public override Calculator GetCalculator(PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForMouseAndTouch;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

    }


    /// <summary>
    /// Vector "drawing" tool for erasing ink storkes
    /// </summary>
    public class VectorEraserTool : VectorSelectionTool
    {
        private static readonly ToolConfig mConfig = new ToolConfig()
        {
            initValue = 2,
            minSpeed = 100,
            maxSpeed = 4000,
            minValue = 2,
            maxValue = 24
        };

        public VectorEraserTool(ManipulationMode mode)
            : base(mode)
        {
        }

        public override bool BlendCurrentStroke => false;

        public override VectorBrush Shape => mCircleBrush;
        protected override ToolConfig SizeConfig => mConfig;

        protected override float PreviousSize { get; set; } = 2f;

        //public event EventHandler EraseFinished;

        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            base.OnReleased(uiElement, args);
            //EraseFinished?.Invoke(this, EventArgs.Empty);
        }


        public override LayoutMask GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return  LayoutMask.X |
                            LayoutMask.Y |
                            LayoutMask.Size;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        public override Calculator GetCalculator(PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForMouseAndTouch;
                default:
                    throw new Exception("Unknown input device type");
            }
        }

    }
}
