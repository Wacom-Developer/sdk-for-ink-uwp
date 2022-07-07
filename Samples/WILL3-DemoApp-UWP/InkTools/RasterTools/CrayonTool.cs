using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;


namespace WacomInkDemoUWP
{
    class CrayonTool : RasterDrawingTool
    {
        public CrayonTool() : base("wdt:Crayon")
        {
            Paint = new Paint(Brushes.Crayon, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
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
                    layoutMask = Layouts.XYSRSxSyOxOy;
                    return CalculatePathPointForPen;
            }

            throw new Exception("Unknown input device type");
        }

        public static PathPoint CalculatePathPointForPen(PointerData previous, PointerData current, PointerData next)
        {
            var pp = new PathPoint(current.X, current.Y)
            {
                Size = 2.0f + 1.0f * current.Force.Value,
                Alpha = current.Force.HasValue ? (0.1f + 0.5f * current.Force.Value) : 0.0f,

                Rotation = current.ComputeNearestAzimuthAngle(previous)
            };

            float cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);

            pp.ScaleX = 1.0f + 12.0f * cosAltitudeAngle;
            pp.OffsetX = 0.5f * 12.0f * cosAltitudeAngle;

            pp.ScaleY = 1.0f;
            pp.OffsetY = 0.0f;

            pp.Blue = Math.Min((float)(1.9f * pp.Alpha), 1);
            pp.Green = Math.Max((float)(0.8f - pp.Alpha), 0.3f);
            pp.Red = 1.0f - pp.Alpha;

            return pp;
        }

        public static PathPoint CalculatePathPointForTouchOrMouse(PointerData previous, PointerData current, PointerData next)
        {
            float? normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.8f, 2.0f);

            if (normVelocity == null)
                return null;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = 20
            };

            return pp;
        }
    }
}
