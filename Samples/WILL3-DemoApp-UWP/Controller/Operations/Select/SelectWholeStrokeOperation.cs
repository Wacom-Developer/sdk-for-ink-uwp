using System.Collections.Generic;
using System.Linq;
using Wacom.Ink;
using Wacom.Ink.Manipulations;
using Windows.UI.Core;


namespace WacomInkDemoUWP
{
    public class SelectWholeStrokeOperation : SelectStrokeOperation
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

            //m_controller.ViewDrawSelectorOverlay(selection.Contours, true);

            PerformSelectOperation(selection);

            m_selectorPath = null;

            // Set redraw flags
            m_controller.ViewInvalidateSceneAndOverlay();

            m_controller.ViewClearCurrentStrokeLayer();
        }

        public void PerformSelectOperation(SelectionContours selection)
        {
            //m_controller.ModelClearCustomShapes();

            SelectWholeStrokeManipulation selector = m_controller.CreateSelectWholeStrokeOperation();

            selector.SelectQuery(selection);

            ProcessSelectWholeStrokeResult(selector.Result);
        }

        public void ProcessSelectWholeStrokeResult(HashSet<IInkStroke> result)
        {
            // Translation (moving) raster strokes is not supported so only select vector strokes
            foreach (Stroke stroke in result.Where(s => s is VectorStroke))
            {
                m_controller.ModelSelectStroke(stroke.Id);
            }
            m_controller.MeasureSelectedStrokes();
        }
    }
}
