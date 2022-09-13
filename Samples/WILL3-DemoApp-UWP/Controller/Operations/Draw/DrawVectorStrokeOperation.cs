using System.Collections.Generic;
using System.Diagnostics;
using Wacom.Ink.Geometry;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public class DrawVectorStrokeOperation : DrawStrokeOperation
    {
        #region Fields

        protected VectorInkBuilderUWP m_inkBuilder = new VectorInkBuilderUWP(true);

        #endregion

        #region Constructors

        public DrawVectorStrokeOperation(InkPanelController controller)
            : base(controller)
        {
            VectorTools = new List<VectorDrawingTool>()
            {
                new PenTool(),
                new FeltTool(),
                new BrushTool(),
                //new FountainPenTool(),
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

        #region Interface

        public readonly List<VectorDrawingTool> VectorTools;

        #region UserOperation API

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

            m_ended = true;
        }

        public override void UpdateView(InkPanelModel model, InkPanelView view)
        {
            view.TryResize();
            view.TryRedrawAllStrokes(model.Strokes);
            view.TryOverlayRedraw(model);

            if (m_inkBuilder.HasNewPoints)
            {
                view.RenderNewVectorStrokeSegment(m_inkBuilder.GetCurrentPolygons(), Color);
            }


            if (m_ended)
            {
                //OperationEnd();
                m_ended = false;
            }
        }

        #endregion

        #endregion

        #region Implementation

        #region DrawStrokeOperation API

        protected override void OnStrokeEnd()
        {
            VectorStroke stroke = m_controller.ModelCreateVectorStroke(m_inkBuilder.SplineAccumulator.Accumulated.Clone(), Color, Tool);

            //m_controller.Replayer.EnqueueCommand(new DrawStrokeCommand(stroke, m_controller));

            SensorDataAccumulator sensorDataAccumulator = m_inkBuilder.SensorDataAccumulator;

            // Store the stroke in the collection
            if (m_keepStroke)
            {
                if (sensorDataAccumulator != null)
                {
                    //sensorDataAccumulator.
                }

                m_controller.ModelStoreStroke(stroke);
                m_controller.ViewDrawCurrentStrokeLayer();
                m_controller.ViewClearCurrentStrokeLayer();
            }

            if (sensorDataAccumulator != null)
            {
                sensorDataAccumulator.Reset();
            }
        }

        protected override void SetupInkTool(PointerEventArgs args)
        {
            //if (UseRandomInkColor)
            //{
            //	Color = Utils.GetRandomColor();
            //}

            Calculator calculator = Tool.GetCalulator(args.CurrentPoint.PointerDevice, out LayoutMask layoutMask);

            m_inkBuilder.UpdateVectorInkPipeline(
                layoutMask,
                calculator,
                Tool.Shape,
                Tool.ConstSize,
                Tool.ConstRotation,
                Tool.Paint.ScaleX,
                Tool.Paint.ScaleY,
                Tool.Paint.OffsetX,
                Tool.Paint.OffsetY);

            //m_inkBuilder.SplineInterpolator.Spacing = Spacing;
            //m_inkBuilder.SplineProducer.KeepAllData = true;
        }

        #endregion

        #endregion
    }
}
