using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;


using Wacom.Ink.Geometry;

namespace Wacom
{
    /// <summary>
    /// Abstract base class for drawing tools used to render ink in a variety of styles
    /// </summary>
    public abstract class DrawingTool
    {
        /// <summary>
        /// Holds configuration parameters for PathPoint calculators
        /// </summary>
        protected class ToolConfig
        {
            public float minSpeed;
            public float maxSpeed;

            public float minValue;
            public float maxValue;
            public Func<float, float> remap;
        };

        /// <summary>
        /// Configuration parameters to use in PathPoint Size calculations
        /// </summary>
        protected abstract ToolConfig SizeConfig { get; }

        /// <summary>
        /// Configuration parameters to use in PathPoint Alpha calculations
        /// </summary>
        /// <remarks>For this sample, Alpha calcuation, based on speed, is demonstrated in <see cref="MouseInputCalculator"/></remarks>
        protected virtual ToolConfig AlphaConfig => null;

        /// <summary>
        /// Fixed value to set PathPointLayout Alpha 
        /// </summary>
        /// <remarks>For this sample, a fixed Alpha is used in <see cref="StylusInputCalculator"/></remarks>
        protected virtual float? Alpha => 1;

        public virtual bool BlendCurrentStroke => true;

        public EventHandler<bool> DrawingFinished;
        public EventHandler PointsAdded;


        public abstract void OnPressed(UIElement uiElement, PointerRoutedEventArgs args);
        public abstract void OnMoved(UIElement uiElement, PointerRoutedEventArgs args);
        public abstract void OnReleased(UIElement uiElement, PointerRoutedEventArgs args);

        /// <summary>
        /// Returns the layout and calculator method to use for input from a stylus (pen)
        /// </summary>
        public virtual (PathPointLayout, Calculator) GetLayoutAndCalulatorForStylus()
        {
            var layout = new PathPointLayout(PathPoint.Property.X,
                                            PathPoint.Property.Y,
                                            PathPoint.Property.Size,
                                            PathPoint.Property.Rotation,
                                            PathPoint.Property.ScaleX,
                                            PathPoint.Property.OffsetX,
                                            PathPoint.Property.Alpha);
            return (layout, StylusInputCalculator);
        }

        /// <summary>
        /// Returns the layout and calculator method to use for input from the mouse
        /// </summary>
        public virtual (PathPointLayout, Calculator) GetLayoutAndCalulatorForMouse()
        {
            var layout = new PathPointLayout(PathPoint.Property.X,
                                            PathPoint.Property.Y,
                                            PathPoint.Property.Size,
                                            PathPoint.Property.Alpha);
            return (layout, MouseInputCalculator);
        }


        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint StylusInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            var size = current.ComputeValueBasedOnSpeed(previous, next, SizeConfig.minValue, SizeConfig.maxValue, null, null, SizeConfig.minSpeed, SizeConfig.maxSpeed, SizeConfig.remap);
            if (size == null)
            {
                return null;
            }

            float cosAltitudeAngle = current.AltitudeAngle.HasValue ? (float)Math.Cos(current.AltitudeAngle.Value) : 0;
            float tiltScale = 0.5f + cosAltitudeAngle;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = current.AzimuthAngle.HasValue ? current.ComputeNearestAzimuthAngle(previous) : 0, 
                ScaleX = tiltScale,
                OffsetX = 0.5f * size * tiltScale,
                Alpha = this.Alpha
            };

            return pp;
        }

        /// <summary>
        /// Calculator delegate for input from mouse input
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        private PathPoint MouseInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            var size = current.ComputeValueBasedOnSpeed(previous, next, SizeConfig.minValue, SizeConfig.maxValue, null, null, SizeConfig.minSpeed, SizeConfig.maxSpeed, SizeConfig.remap);

            if (size == null)
            {
                return null;
            }

            float? alpha = (AlphaConfig != null)
                    ? current.ComputeValueBasedOnSpeed(previous, next, AlphaConfig.minValue, AlphaConfig.maxValue, null, null, AlphaConfig.minSpeed, AlphaConfig.maxSpeed, AlphaConfig.remap)
                    : Alpha;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Alpha = alpha
            };

            return pp;
        }

    }
}
