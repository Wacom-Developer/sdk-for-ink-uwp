using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    abstract class RasterDrawingTool : DrawingTool
    {
        public RasterDrawingTool(string uri) : base(uri)
        {
        }

        public ParticleBrush RasterBrush
        {
            get
            {
                return ((AppRasterBrush)Brush).ParticleBrush;
            }
        }
    }
}
