using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class FountainPenTool : VectorDrawingTool
    {
        public FountainPenTool() : base("wdt:FountainPen")
        {
            Paint = new Paint(Brushes.VerticalEllipse, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
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
                    layoutMask = Layouts.XYSR;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.8f, 2.0f);

            if (normVelocity == null)
                return null;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = normVelocity.Value
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            if (current.Force == null)
                throw new Exception("current.Force is null");

            if (current.AltitudeAngle == null)
                throw new Exception("current.AltitudeAngle is null");

            const float minValue = 1.0f;
            const float maxValue = 2.0f;
            const float initialValue = 1.2f;

            float? speedValue = current.ComputeValueBasedOnSpeed(previous, next, minValue, maxValue, initialValue, null, 100.0f, 2000.0f);
            float x = speedValue.Value;
            float x2 = x * x;

            // https://arachnoid.com/polysolve/
            double speedResponse =
                  5.0283333106664450e+001
                - 1.1320753903339693e+002 * x
                + 9.6288888207406060e+001 * x2
                - 3.6263888577868308e+001 * x2 * x
                + 5.0992062970889629e+000 * x2 * x2;

            float force = current.Force.Value;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = 1.0f * ((float)speedResponse) + 19.2f * force + 0.1f,
                Rotation = current.ComputeNearestAzimuthAngle(previous)
            };

            return pp;
        }
    }
}
