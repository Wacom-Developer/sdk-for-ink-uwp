using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;


namespace WacomInkDemoUWP
{
    class CrayonTool : RasterDrawingTool
    {
        public CrayonTool() : base("wdt:Crayon")
        {
            Brush = Brushes.Crayon;
		}

        public override Calculator GetCalulator(Windows.Devices.Input.PointerDevice device, out LayoutMask layoutMask)
        {
            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutMask = Layouts.XYS;
                    return CalculatePathPointForTouchOrMouse;

                case Windows.Devices.Input.PointerDeviceType.Pen:
					layoutMask = Layouts.XYSRCaSy;
					return CalculatePathPointForPen;
            }

            throw new Exception("Unknown input device type");
        }

		public static PathPoint CalculatePathPointForTouchOrMouse(PointerData previous, PointerData current, PointerData next)
		{
			PathPoint pp = new PathPoint(current.X, current.Y)
			{
				Size = 20
			};

			return pp;
		}

		public static PathPoint CalculatePathPointForPen(PointerData previous, PointerData current, PointerData next)
        {
			float pressure = current.Force ?? 1.0f;

			float azimuth = current.ComputeNearestAzimuthAngle(previous) ?? 0.0f;

            PathPoint output = new PathPoint(current.X, current.Y);
			output.Rotation = azimuth;
			output.Size = 9.0f + 2.4f * pressure;
			output.Alpha = 0.1f + 0.9f * pressure;
			output.ScaleY = 1.0f + 1.2f * pressure;

			return output;
		}
    }
}
