using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class BallPenTool : VectorDrawingTool
    {
        public BallPenTool() : base("wdt:BallPen")
        {
            Brush = Brushes.Circle;
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
                    layoutMask = Layouts.XYS;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseAndTouchInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 1.0f, 2.0f) ?? 1.5f;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = MathFunctions.MapTo(normVelocity, new Range(1.0f, 2.0f), new Range(0.4f, 1.0f), (v) => MathFunctions.Sigmoid1(v, 0.62f))
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float pressure = current.Force ?? 0.8f;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = 1.0f + 1.5f * pressure,
            };

            return pp;
        }
    }
}
