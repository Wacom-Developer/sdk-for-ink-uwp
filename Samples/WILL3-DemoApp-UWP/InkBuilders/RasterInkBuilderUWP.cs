using Wacom.Ink.Geometry;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class RasterInkBuilderUWP : StockRasterInkBuilder
    {
        public bool UseIntermediatePoints { get; set; } = true;

        public RasterInkBuilderUWP()
        {
        }

        public void UpdateParticleInkPipeline(LayoutMask layoutMask, Calculator calculator, float spacing, float constSize = 1.0f)
        {
            PathProducer.LayoutMask = layoutMask;
            PathProducer.PathPointCalculator = calculator;

            SplineInterpolator.Spacing = spacing;
            SplineInterpolator.DefaultSize = constSize;
        }

        public void AddPointsFromEvent(Phase phase, PointerEventArgs args)
        {
            PointerDataProvider.AddPointsFromEvent(phase, args, UseIntermediatePoints);
        }
    }
}