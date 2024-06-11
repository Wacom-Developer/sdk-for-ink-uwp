using System;
using System.Collections.Generic;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class DrawRasterStrokeOperation : DrawStrokeOperation
    {
        #region Fields

        private readonly RasterInkBuilderUWP m_inkBuilder = new RasterInkBuilderUWP();
        private readonly Random m_rand = new Random();
        private uint m_startRandomSeed;
        private DrawStrokeResult m_drawStrokeResult;

		public readonly List<RasterDrawingTool> RasterTools = new List<RasterDrawingTool>()
		{
			new CrayonTool(),
			new PencilTool(),
			new WaterBrushTool()
		};

		#endregion

		#region Constructors

		public DrawRasterStrokeOperation(InkPanelController controller)
            : base(controller)
        {
        }

        #endregion

        #region Properties

        public RasterDrawingTool Tool { get; set; }

        public uint RandomSeed
        {
            get => m_startRandomSeed;
        }

        #endregion

        #region Overrides from UserOperation

        public override void OnPointerPressed(PointerEventArgs args)
        {
            m_controller.ViewCaptureInputPointer();
            SetupInkTool(args);
            m_startRandomSeed = (uint)m_rand.Next();
            m_drawStrokeResult.RandomGeneratorSeed = m_startRandomSeed;
            m_inkBuilder.AddPointsFromEvent(Phase.Begin, args);
        }

        public override void OnPointerMoved(PointerEventArgs args)
        {
            m_inkBuilder.AddPointsFromEvent(Phase.Update, args);
        }

        public override void OnPointerReleased(PointerEventArgs args)
        {
            m_controller.ViewReleaseInputPointer();
            m_inkBuilder.AddPointsFromEvent(Phase.End, args);

            m_controller.ViewRenderNewRasterStrokeSegment(m_inkBuilder.GetCurrentInterpolatedPaths(), Tool, Color, ref m_drawStrokeResult);

            OnStrokeEnd();
		}

        public override void UpdateView(InkPanelModel model, InkPanelView view)
        {
            view.TryResize();
            view.TryRedrawAllStrokes(model.Strokes);

            if (m_inkBuilder.HasNewPoints)
            {
                view.RenderNewRasterStrokeSegment(m_inkBuilder.GetCurrentInterpolatedPaths(), Tool, Color, ref m_drawStrokeResult);
            }
        }

        #endregion

        #region Implementation

        protected override void OnStrokeEnd()
        {
            RasterStroke stroke = CreateRasterStroke(
                m_inkBuilder.SplineAccumulator.Accumulated.Clone(),
                Color,
                Tool,
                RandomSeed);

            // Store the stroke in the collection
            m_controller.ModelStoreStroke(stroke);
            m_controller.ViewDrawCurrentStrokeLayer();
            m_controller.ViewClearCurrentStrokeLayer();

			m_controller.ResetOperation();
		}

        protected override void SetupInkTool(PointerEventArgs args)
        {
            Calculator calculator = Tool.GetCalulator(args.CurrentPoint.PointerDevice, out LayoutMask layoutMask);

            m_inkBuilder.UpdateParticleInkPipeline(layoutMask, calculator, Tool.ParticleSpacing);

            m_inkBuilder.SplineProducer.KeepAllData = true;
        }

        #endregion
    }
}
