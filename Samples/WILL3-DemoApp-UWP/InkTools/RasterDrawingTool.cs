using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    public abstract class RasterDrawingTool : DrawingTool
    {
        public RasterDrawingTool(string uri) : base(uri)
        {
        }

        public ParticleBrush RasterBrush
        {
            get
            {
                return ((AppRasterBrush)Paint.Brush).ParticleBrush;
            }
        }
    }
}
