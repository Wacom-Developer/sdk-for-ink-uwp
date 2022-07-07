using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class PencilTool : RasterDrawingTool
    {
        public PencilTool() : base("wdt:Pencil")
        {
            Paint = new Paint(Brushes.Pencil, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
        }

        public override Calculator GetCalulator(Windows.Devices.Input.PointerDevice device, out LayoutMask layoutMask)
        {
            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutMask = Layouts.XYSCa;
                    return MouseAndTouchInputCalculator;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutMask = Layouts.XYSRCaSxOx;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseAndTouchInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.0f, 1.0f);

            if (normVelocity == null)
                return null;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = MathFunctions.MapTo(normVelocity.Value, new Range(0.0f, 1.0f), new Range(4.0f, 5.0f)),
                Alpha = MathFunctions.MapTo(normVelocity.Value, new Range(0.0f, 1.0f), new Range(0.05f, 1.0f))
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.0f, 1.0f);

            if (normVelocity == null)
                return null;

            float cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            float tiltScale = 0.5f + cosAltitudeAngle;
            float size = MathFunctions.MapTo(normVelocity.Value, new Range(0.0f, 1.0f), new Range(4.0f, 5.0f));

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size
            };

            if (current.Force.HasValue)
            {
                pp.Alpha = MathFunctions.MapTo(current.Force.Value, new Range(0.0f, 1.0f), new Range(0.1f, 1.0f));
            }
            else
            {
                pp.Alpha = MathFunctions.MapTo(normVelocity.Value, new Range(0.0f, 1.0f), new Range(0.05f, 1.0f));
            }

            pp.Rotation = current.ComputeNearestAzimuthAngle(previous);
            pp.ScaleX = tiltScale;
            pp.OffsetX = 0.5f * size * tiltScale;

            return pp;
        }
    }
}
