using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Wacom.Ink;
using Wacom.Ink.Manipulations;
using Windows.UI.Core;


namespace WacomInkDemoUWP
{
    class SelectWholeStrokeOperation : SelectStrokeOperation
    {
        #region Constructors

        public SelectWholeStrokeOperation(InkPanelController controller)
            : base(controller)
        {
        }

        #endregion

        public override void OnPointerReleased(PointerEventArgs args)
        {
            AddPointToSelectorPath(args.CurrentPoint);
            base.OnPointerReleased(args);
        }

        protected override void OnStrokeEnd()
        {
            SelectionContours selection = SelectionContours.FromPath(m_selectorPath);

            PerformSelectOperation(selection);

			if (m_controller.Selection.Count > 0)
			{
				m_controller.CurrentOperation = m_controller.MoveSelectedStrokesOp;
			}
			else
			{
				m_controller.CurrentOperation = m_controller.IdleOp;
			}

			m_selectorPath = null;

            // Set redraw flags
            m_controller.ViewInvalidateSceneAndOverlay();

            m_controller.ViewClearCurrentStrokeLayer();
        }

        public void PerformSelectOperation(SelectionContours selection)
        {
            SelectWholeStrokeManipulation selector = m_controller.CreateSelectWholeStrokeOperation();

            selector.SelectQuery(selection);

            // Process the result of the select query
			foreach (Stroke stroke in selector.Result)
			{
				// Selection of raster strokes is not supported, only vector strokes are expected in the result
				Debug.Assert(stroke is VectorStroke);

				m_controller.Selection.Add(stroke.Id);
			}

			m_controller.MeasureSelectedStrokes();
		}
    }
}
