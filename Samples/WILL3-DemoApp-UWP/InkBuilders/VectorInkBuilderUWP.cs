using System.Numerics;
using Wacom.Ink.Geometry;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class VectorInkBuilderUWP : StockVectorInkBuilder
    {
        public bool UseIntermediatePoints { get; set; } = true;

        public VectorInkBuilderUWP()
        {
        }

        public void UpdateVectorInkPipeline(
            LayoutMask layoutMask,
            Calculator calculator,
            VectorBrush brush,
            float constSize = 1.0f,
            float constRotation = 0.0f,
            float scaleX = 1.0f,
            float scaleY = 1.0f,
            float offsetX = 0.0f,
            float offsetY = 0.0f)
        {
            PathProducer.LayoutMask = layoutMask;
            PathProducer.PathPointCalculator = calculator;

            BrushApplier.Prototype = brush;
            BrushApplier.DefaultSize = constSize;
            BrushApplier.DefaultRotation = constRotation;
            BrushApplier.DefaultScale = new Vector3(scaleX, scaleY, 1.0f);
            BrushApplier.DefaultOffset = new Vector3(offsetX, offsetY, 0.0f);
        }

        public void AddPointsFromEvent(Phase phase, PointerEventArgs args)
        {
            PointerDataProvider.AddPointsFromEvent(phase, args, UseIntermediatePoints);
        }
    }
}
