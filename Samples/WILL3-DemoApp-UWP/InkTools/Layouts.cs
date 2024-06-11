using Wacom.Ink.Geometry;


namespace WacomInkDemoUWP
{
    static class Layouts
    {
		public static readonly LayoutMask XY =
	        LayoutMask.X |
	        LayoutMask.Y;

		public static readonly LayoutMask XYS =
	        LayoutMask.X |
	        LayoutMask.Y |
	        LayoutMask.Size;

		public static readonly LayoutMask XYSR =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation;

		public static readonly LayoutMask XYSCa =
			LayoutMask.X |
			LayoutMask.Y |
			LayoutMask.Size |
			LayoutMask.Alpha;

		public static readonly LayoutMask XYSRSxOx =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.ScaleX |
            LayoutMask.OffsetX;

		public static readonly LayoutMask XYSRCaSy =
	        LayoutMask.X |
	        LayoutMask.Y |
	        LayoutMask.Size |
	        LayoutMask.Rotation |
	        LayoutMask.Alpha |
	        LayoutMask.ScaleY;

		public static readonly LayoutMask XYSCaSyOy =
			LayoutMask.X |
			LayoutMask.Y |
			LayoutMask.Size |
			LayoutMask.Alpha |
			LayoutMask.ScaleY |
			LayoutMask.OffsetY;

		public static readonly LayoutMask XYSRCaSxOx =
            LayoutMask.X |
            LayoutMask.Y |
            LayoutMask.Size |
            LayoutMask.Rotation |
            LayoutMask.Alpha |
            LayoutMask.ScaleX |
            LayoutMask.OffsetX;
    }
}
