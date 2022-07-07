using Wacom.Ink.Geometry;
using Windows.Devices.Input;

namespace WacomInkDemoUWP
{
    public abstract class DrawingTool
    {
        public float ConstSize { get; protected set; } = 1.0f;
        public float ConstRotation { get; protected set; } = 0.0f;
        public Paint Paint { get; protected set; }
        public string Uri { get; private set; }

        public DrawingTool(string uri)
        {
            Uri = uri;
        }

        public float ParticleSpacing
        {
            get => Paint.Brush.Spacing;
        }

        public VectorBrush Shape
        {
            get => Paint.Brush.VectorBrush;
        }

        public abstract Calculator GetCalulator(PointerDevice device, out LayoutMask layoutMask);
    }
}

