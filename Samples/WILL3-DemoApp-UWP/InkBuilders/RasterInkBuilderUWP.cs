using Wacom.Ink.Geometry;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class RasterInkBuilderUWP : StockRasterInkBuilder
    {
        public bool UseIntermediatePoints { get; set; }

        public SensorDataAccumulator SensorDataAccumulator { get; set; }

        public RasterInkBuilderUWP(bool storeSensorData = false)
        {
            UseIntermediatePoints = true;

            if (storeSensorData)
            {
                SensorDataAccumulator = new SensorDataAccumulator();
                SensorDataAccumulator.SetDataProvider(PointerDataProvider);
            }
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

            if (SensorDataAccumulator != null)
            {
                SensorDataAccumulator.Process();
            }
        }

    }
}