using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class FountainPenTool : VectorDrawingTool
    {
        public FountainPenTool() : base("wdt:FountainPen")
        {
            Brush = Brushes.Ellipse;
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
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.8f, 2.0f) ?? 1.6f;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = normVelocity
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            const float minValue = 1.0f;
            const float maxValue = 2.0f;
            const float initialValue = 1.2f;

			float pressure = current.Force ?? 0.8f;
            float normSpeed = current.ComputeValueBasedOnSpeed(previous, next, minValue, maxValue, initialValue, null, 100.0f, 4000.0f) ?? initialValue;

            float azimuth = current.ComputeNearestAzimuthAngle(previous) ?? 0.0f;

			azimuth += (float)(Math.PI * 0.5f);

			float speedResponse = 1.0f / (1.2f * normSpeed);

            PathPoint pp = new PathPoint(current.X, current.Y);
            pp.Size = speedResponse * (6.0f * pressure) + 0.5f;
            pp.Rotation = azimuth;

            return pp;
        }
    }
}
