using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class CircleSelectorTool : VectorDrawingTool
    {
        public CircleSelectorTool() : base("wdt:CircleSelector")
        {
            Brush = Brushes.Circle;
            ConstSize = 2.0f;
        }

        public override Calculator GetCalulator(Windows.Devices.Input.PointerDevice device, out LayoutMask layoutMask)
        {
			layoutMask = Layouts.XY;
			return CalculatePathPoint;
        }

        public static PathPoint CalculatePathPoint(PointerData previous, PointerData current, PointerData next)
        {
            return new PathPoint(current.X, current.Y);
        }
    }
}