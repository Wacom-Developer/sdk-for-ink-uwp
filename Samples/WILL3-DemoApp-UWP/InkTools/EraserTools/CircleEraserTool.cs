﻿using System;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    class CircleEraserTool : VectorDrawingTool
    {
        private const float sizeMin = 9f;
        private const float sizeMultiplier = 30f;

        public CircleEraserTool() : base("wdt:CircleEraser")
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
                    return CalculatePathPointForTouchOrMouse;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutMask = Layouts.XYS;
                    return CalculatePathPointForPen;
            }

            throw new Exception("Unknown input device type");
        }

        public static PathPoint CalculatePathPointForTouchOrMouse(PointerData previous, PointerData current, PointerData next)
        {
            float normVelocity = current.ComputeValueBasedOnSpeed(previous, next, 0.0f, 1.0f) ?? 0.5f;

            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = sizeMin + sizeMultiplier * normVelocity
            };

            return pp;
        }

        public static PathPoint CalculatePathPointForPen(PointerData previous, PointerData current, PointerData next)
        {
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = sizeMin + sizeMultiplier * current.Force.Value
            };

            return pp;
        }
    }
}
