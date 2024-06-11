using Wacom.Ink.Geometry;
using Windows.Devices.Input;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    abstract class DrawingTool
    {
		public string Uri { get; private set; }

        public float ConstSize { get; protected set; } = 1.0f;
        public float ConstRotation { get; protected set; } = 0.0f;

		public AppBrush Brush { get; protected set; }
		public string BrushUri { get; protected set; }
		public float ScaleX { get; protected set; } = 1.0f;
		public float ScaleY { get; protected set; } = 1.0f;
		public float OffsetX { get; protected set; } = 0.0f;
		public float OffsetY { get; protected set; } = 0.0f;
		public BlendMode StrokeBlendMode { get; set; } = BlendMode.SourceOver;

		public DrawingTool(string uri)
        {
            Uri = uri;
        }

        public float ParticleSpacing
        {
            get => Brush.Spacing;
        }

        public VectorBrush Shape
        {
            get => Brush.VectorBrush;
        }

        public abstract Calculator GetCalulator(PointerDevice device, out LayoutMask layoutMask);
    }
}

