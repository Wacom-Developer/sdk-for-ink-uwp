using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class BrushTool : VectorDrawingTool
    {
        public BrushTool() : base("wdt:Brush")
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
                    return MouseInputCalculator;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutMask = Layouts.XYSRSxOx;
                    return PenInputCalculator;
            }

            throw new Exception("Unknown input device type");
        }

		public static PathPoint MouseInputCalculator(PointerData previous, PointerData current, PointerData next)
		{
			float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.8f, 2.0f) ?? 1.5f;

			PathPoint pp = new PathPoint(current.X, current.Y)
			{
				Size = 20 * normVelocity
			};

			return pp;
		}

		public static PathPoint PenInputCalculator(PointerData previous, PointerData current, PointerData next)
        {
            float pressure = current.Force ?? 0.8f;
			float altitude = current.AltitudeAngle ?? (float)(Math.PI * 0.5);
            
            float cosAltitudeAngle = (float)Math.Cos(altitude);
            float tiltScale = 2.0f * cosAltitudeAngle;
            float size = 1.0f + 20.0f * pressure;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Rotation = current.ComputeNearestAzimuthAngle(previous),

                ScaleX = 1.0f + tiltScale,
                OffsetX = 0.5f * size * tiltScale
            };

            return pp;
        }
    }
}
