using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    public class PenTool : VectorDrawingTool
    {
        public PenTool() : base("wdt:Pen")
        {
            Paint = new Paint(Brushes.Circle, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
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
                Size = MathFunctions.MapTo(normVelocity.Value, new Range(1.0f, 2.0f), new Range(0.4f, 1.0f), (v) => MathFunctions.Sigmoid1(v, 0.62f))
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            if (current.Force == null)
            {
                throw new Exception("current.Force is null");
            }

            if (current.AltitudeAngle == null)
            {
                throw new Exception("current.AltitudeAngle is null");
            }

            float cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            float tiltScale = 0.5f + cosAltitudeAngle;
            float size = 0.4f + current.Force.Value;

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
