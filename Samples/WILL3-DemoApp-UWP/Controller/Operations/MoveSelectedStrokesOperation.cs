using System;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    class MoveSelectedStrokesOperation : UserOperation
    {
        private Point m_origin;
		private Vector2 m_offsetFromOrigin = Vector2.Zero;

        public MoveSelectedStrokesOperation(InkPanelController controller)
            : base(controller)
        {
        }

        public override void OnPointerPressed(PointerEventArgs args)
        {
            m_origin = args.CurrentPoint.Position;
			m_offsetFromOrigin = Vector2.Zero;

            m_controller.ViewInvalidateSceneAndOverlay();			
		}

        public override void OnPointerMoved(PointerEventArgs args)
        {
			CalculateOffsetFromOrigin(args.CurrentPoint.Position);

			m_controller.ViewInvalidateOverlay();
		}

        public override void OnPointerReleased(PointerEventArgs args)
        {
			CalculateOffsetFromOrigin(args.CurrentPoint.Position);

            m_controller.ModelMoveSelectedStrokes(m_offsetFromOrigin);

			m_offsetFromOrigin = Vector2.Zero;

			m_controller.ViewInvalidateOverlay();
		}

		public override void UpdateView(InkPanelModel model, InkPanelView view)
        {
			view.TryResize();

			Func<Stroke, bool> filter = m_controller.Selection.GetNotSelectedStrokesFilter();

			view.TryRedrawAllStrokes(model.Strokes, filter);
			view.TryOverlayRedraw(model, m_controller.Selection, m_offsetFromOrigin);
		}

		public override UserOperation DetermineCurrentOperation(PointerEventArgs args)
		{
            if (m_controller.Selection.HitTest(args.CurrentPoint.Position))
            {
				// Begin move operation
				return this;
			}

			m_controller.ClearSelection();

			return m_controller.IdleOp.DetermineCurrentOperation(args);
		}

		private void CalculateOffsetFromOrigin(Point p)
        {
			m_offsetFromOrigin = new Vector2((float)(p.X - m_origin.X), (float)(p.Y - m_origin.Y));
        }
    }
}
