using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class PencilTool : RasterDrawingTool
    {
        public PencilTool() : base("wdt:Pencil")
        {
            Brush = Brushes.Pencil;
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
					layoutMask = Layouts.XYSCaSyOy;
					return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

        static PathPoint MouseAndTouchInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.0f, 1.0f) ?? 0.5f;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = MathFunctions.MapTo(normVelocity, new Range(0.0f, 1.0f), new Range(4.0f, 5.0f)),
                Alpha = MathFunctions.MapTo(normVelocity, new Range(0.0f, 1.0f), new Range(0.05f, 1.0f))
            };

            return pp;
        }

        static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
			float pressure = current.Force ?? 0.8f;

			float cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
			float tiltScale = 0.5f + cosAltitudeAngle;

			float size = MathFunctions.MapTo(pressure, new Range(0.0f, 1.0f), new Range(1.0f, 5.0f));

            PathPoint pp = new PathPoint(current.X, current.Y);
			pp.Size = size;

			float alpha = 0.14f + 20.0f * (float)Math.Exp(5.0 * pressure - 7.6);

			if (alpha > 1.0f)
				alpha = 1.0f;

			pp.Alpha = alpha;

			pp.ScaleY = tiltScale;
			pp.OffsetY = 0.5f * size * tiltScale;

			return pp;
        }
    }
}
