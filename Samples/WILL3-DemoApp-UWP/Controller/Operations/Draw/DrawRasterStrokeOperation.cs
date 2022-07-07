using System;
using System.Collections.Generic;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public class DrawRasterStrokeOperation : DrawStrokeOperation
    {
        #region Fields

        private readonly RasterInkBuilderUWP m_inkBuilder = new RasterInkBuilderUWP(true);
        private readonly Random m_rand = new Random();
        private uint m_startRandomSeed;
        private DrawStrokeResult m_drawStrokeResult;

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
            get
            {
                return m_startRandomSeed;
            }
        }

        #endregion

        #region Interface

        public readonly List<RasterDrawingTool> RasterTools = new List<RasterDrawingTool>()
        {
            new CrayonTool(),
            new PencilTool(),
            new WaterBrushTool()
        };

        #region UserOperation API

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
            // TODO: Maybe ViewRenderNewRasterStrokeSegment?
            m_controller.ViewRenderNewRasterStrokeSegment(m_inkBuilder.GetCurrentInterpolatedPaths(), Tool, Color, ref m_drawStrokeResult);
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
                view.RenderNewRasterStrokeSegment(m_inkBuilder.GetCurrentInterpolatedPaths(), Tool, Color, ref m_drawStrokeResult);
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

        protected override void OnStrokeEnd()
        {
            // TODO: Maybe pass here what's necessary to build cache in CreateRasterStroke?
            // TODO: Do we pass the entire operation or just the necessary stuff (paint, seed, color)

            RasterStroke stroke = m_controller.ModelCreateRasterStroke(
                m_inkBuilder.SplineAccumulator.Accumulated.Clone(),
                Color,
                Tool,
                RandomSeed,
                m_tag);

            //m_controller.Replayer.EnqueueCommand(new DrawStrokeCommand(stroke, m_controller));

            // Store the stroke in the collection
            if (m_keepStroke)
            {
                m_controller.ModelStoreStroke(stroke);
                m_controller.ViewDrawCurrentStrokeLayer();
                m_controller.ViewClearCurrentStrokeLayer();
            }
        }

        protected override void SetupInkTool(PointerEventArgs args)
        {
            //if (UseRandomInkColor)
            //{
            //	Color = Utils.GetRandomColor();
            //}

            Calculator calculator = Tool.GetCalulator(args.CurrentPoint.PointerDevice, out LayoutMask layoutMask);

            m_inkBuilder.UpdateParticleInkPipeline(layoutMask, calculator, Tool.ParticleSpacing);

            //m_inkBuilder.SplineInterpolator.Spacing = Spacing;
            m_inkBuilder.SplineProducer.KeepAllData = true;
        }

        #endregion
    }
}
