using System.Collections.Generic;
using System.Diagnostics;
using Wacom.Ink.Geometry;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class DrawVectorStrokeOperation : DrawStrokeOperation
    {
        #region Fields

        protected VectorInkBuilderUWP m_inkBuilder = new VectorInkBuilderUWP();

		public readonly List<VectorDrawingTool> VectorTools;

		#endregion

		#region Constructors

		public DrawVectorStrokeOperation(InkPanelController controller)
            : base(controller)
        {
            VectorTools = new List<VectorDrawingTool>()
            {
                new BallPenTool(),
                new FountainPenTool(),
                new BrushTool(),
            };

            Tool = VectorTools[0];
        }

        protected DrawVectorStrokeOperation(InkPanelController controller, List<VectorDrawingTool> tools, int defaultToolIndex)
            : base(controller)
        {
            VectorTools = tools;
            Tool = VectorTools[defaultToolIndex];
        }

		#endregion

		#region Properties

		public VectorDrawingTool Tool { get; set; }

		#endregion

		#region Overrides from UserOperation

		public override void OnPointerPressed(PointerEventArgs args)
        {
            m_controller.ViewCaptureInputPointer();
            SetupInkTool(args);
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
            m_controller.ViewRenderNewVectorStrokeSegment(m_inkBuilder.GetCurrentPolygons(), Color);

            OnStrokeEnd();
		}

        public override void UpdateView(InkPanelModel model, InkPanelView view)
        {
            view.TryResize();
            view.TryRedrawAllStrokes(model.Strokes);

            if (m_inkBuilder.HasNewPoints)
            {
                view.RenderNewVectorStrokeSegment(m_inkBuilder.GetCurrentPolygons(), Color);
            }
        }

        #endregion

        #region Overrides from DrawStrokeOperation

        protected override void OnStrokeEnd()
        {
            VectorStroke stroke = CreateVectorStroke(m_inkBuilder.SplineAccumulator.Accumulated.Clone(), Color, Tool);

            // Store the stroke in the model
            m_controller.ModelStoreStroke(stroke);
            m_controller.ViewDrawCurrentStrokeLayer();
            m_controller.ViewClearCurrentStrokeLayer();

			m_controller.ResetOperation();
		}

        protected override void SetupInkTool(PointerEventArgs args)
        {
            Calculator calculator = Tool.GetCalulator(args.CurrentPoint.PointerDevice, out LayoutMask layoutMask);

            m_inkBuilder.UpdateVectorInkPipeline(
                layoutMask,
                calculator,
                Tool.Shape,
                Tool.ConstSize,
                Tool.ConstRotation,
                Tool.ScaleX,
                Tool.ScaleY,
                Tool.OffsetX,
                Tool.OffsetY);
        }

        #endregion
    }
}
