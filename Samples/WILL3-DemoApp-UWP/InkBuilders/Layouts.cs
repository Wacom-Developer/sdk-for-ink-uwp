using Wacom.Ink.Geometry;


namespace WacomInkDemoUWP
{
    public static class Layouts
    {
        public static readonly LayoutMask XYSR =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation;

        public static readonly LayoutMask XYSOxOy =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.OffsetX |
            LayoutMask.OffsetY;

        public static readonly LayoutMask XYSRSxSyOxOy =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.ScaleX |
            LayoutMask.ScaleY |
            LayoutMask.OffsetX |
            LayoutMask.OffsetY;

        public static readonly LayoutMask XYSRCaSxSyOxOy =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.Alpha |
            LayoutMask.ScaleX |
            LayoutMask.ScaleY |
            LayoutMask.OffsetX |
            LayoutMask.OffsetY;

        public static readonly LayoutMask XYSRSxOx =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.ScaleX |
            LayoutMask.OffsetX;

        public static readonly LayoutMask XYS =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size;

        public static readonly LayoutMask XYSCa =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Alpha;

        public static readonly LayoutMask XYSRCaSxOx =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.Alpha |
            LayoutMask.ScaleX |
            LayoutMask.OffsetX;

        public static readonly LayoutMask XYCrCgCbCa =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Red |
            LayoutMask.Green |
            LayoutMask.Blue |
            LayoutMask.Alpha;

        public static readonly LayoutMask XYSCrCgCbCa =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Red |
            LayoutMask.Green |
            LayoutMask.Blue |
            LayoutMask.Alpha;

        public static readonly LayoutMask XYSRCrCgCbCaSxOx =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.Red |
            LayoutMask.Green |
            LayoutMask.Blue |
            LayoutMask.Alpha |
            LayoutMask.ScaleX |
            LayoutMask.OffsetX;

        public static readonly LayoutMask XY =
            LayoutMask.X |
            LayoutMask.Y;
    }
}
