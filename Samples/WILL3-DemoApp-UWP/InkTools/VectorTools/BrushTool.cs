using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    public class BrushTool : VectorDrawingTool
    {
        public BrushTool() : base("wdt:Brush")
        {
            Paint = new Paint(Brushes.Circle, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);

            //ConstRotation = (float)(0.25 * Math.PI);
            //ConstRotation = (float)(0.75 * Math.PI);
        }

        public override Calculator GetCalulator(Windows.Devices.Input.PointerDevice device, out LayoutMask layoutMask)
        {
            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutMask = Layouts.XYS;
                    return MouseInputCalculator;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutMask = Layouts.XYSRSxOx;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        public static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
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
            float tiltScale = 2.0f * cosAltitudeAngle;
            float size = 3.0f + 20.0f * current.Force.Value;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = current.ComputeNearestAzimuthAngle(previous),

                ScaleX = 1.0f + tiltScale,
                OffsetX = 0.5f * size * tiltScale
            };

            return pp;
        }

        public static PathPoint MouseInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.8f, 2.0f);

            if (normVelocity == null)
                return null;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = 20 * normVelocity.Value
            };

            return pp;
        }
    }
}
