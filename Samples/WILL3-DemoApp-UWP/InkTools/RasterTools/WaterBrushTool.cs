using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    public class WaterBrushTool : RasterDrawingTool
    {
        public WaterBrushTool() : base("wdt:WaterBrush")
        {
            Paint = new Paint(Brushes.Water, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
        }

        public override Calculator GetCalulator(Windows.Devices.Input.PointerDevice device, out LayoutMask layoutMask)
        {
            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutMask = Layouts.XYS;
                    return MouseAndTouchInputCalculator;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutMask = Layouts.XYSRSxOx;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseAndTouchInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 1.0f, 2.0f);

            if (normVelocity == null)
                return null;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = MathFunctions.MapTo(normVelocity.Value, new Range(1.0f, 2.0f), new Range(20.0f, 55.0f))
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 1.0f, 2.0f);

            if (normVelocity == null)
                return null;

            float cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            float tiltScale = 0.5f + cosAltitudeAngle;
            float size = MathFunctions.MapTo(normVelocity.Value, new Range(1.0f, 2.0f), new Range(10.0f, 20.0f), (v) => MathFunctions.Sigmoid1(v, 0.62f));

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = current.ComputeNearestAzimuthAngle(previous),

                ScaleX = tiltScale,
                OffsetX = 0.5f * size * tiltScale
            };

            return pp;
        }
    }
}
