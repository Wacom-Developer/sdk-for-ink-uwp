using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    public class CircleSelectorTool : VectorDrawingTool
    {
        private const float sizeMin = 2f;

        public CircleSelectorTool() : base("wdt:CircleSelector")
        {
            Paint = new Paint(Brushes.Circle, 1.0f, 1.0f, 0.0f, 0.0f, BlendMode.SourceOver);
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
                    layoutMask = Layouts.XYS;
                    return CalculatePathPointForPen;
            }

            throw new Exception("Unknown input device type");
        }

        public static PathPoint CalculatePathPointForTouchOrMouse(PointerData previous, PointerData current, PointerData next)
        {
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = sizeMin
            };

            return pp;
        }

        public static PathPoint CalculatePathPointForPen(PointerData previous, PointerData current, PointerData next)
        {
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = sizeMin
            };

            return pp;
        }
    }
}
