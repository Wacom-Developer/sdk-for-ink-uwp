using System;
using Wacom.Ink.Geometry;

namespace WacomInkDemoUWP
{
    class WaterBrushTool : RasterDrawingTool
    {
        public WaterBrushTool() : base("wdt:WaterBrush")
        {
            Brush = Brushes.Water;
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
                    layoutMask = Layouts.XYSRCaSxOx;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseAndTouchInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 1.0f, 2.0f) ?? 1.5f;

            PathPoint pp = new PathPoint(current.X, current.Y);
            pp.Size = MathFunctions.MapTo(normVelocity, new Range(1.0f, 2.0f), new Range(20.0f, 55.0f));

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.5f, 0.8f) ?? 0.7f;
			float pressure = current.Force ?? 0.8f;
			float rotation = current.ComputeNearestAzimuthAngle(previous) ?? 0.0f;
            float altitude = current.AltitudeAngle ?? (float)(Math.PI * 0.35);

            float tiltScale = 0.5f + (float)Math.Cos(altitude);
			float size = MathFunctions.MapTo(pressure, new Range(0.0f, 1.0f), new Range(2.0f, 40.0f), (v) => MathFunctions.Sigmoid1(v, 0.62f));

			PathPoint pp = new PathPoint(current.X, current.Y);
            pp.Size = size;
            pp.Rotation = rotation;
            pp.ScaleX = tiltScale;
            pp.OffsetX = 0.5f * size * tiltScale;
			pp.Alpha = 1.0f - normVelocity;

            return pp;
        }
    }
}