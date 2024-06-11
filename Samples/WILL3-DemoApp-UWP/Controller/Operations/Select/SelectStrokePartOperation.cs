using System.Diagnostics;
using System.Linq;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
	class SelectStrokePartOperation : SelectStrokeOperation
    {
        #region Constructors

        public SelectStrokePartOperation(InkPanelController controller)
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

        public void PerformSelectOperation(SelectionContours selectionContours)
        {
            SelectStrokePartManipulation selector = m_controller.CreateSelectStrokePartOperation();

            selector.SelectQuery(selectionContours);

            ProcessSelectorResult(selector.Result);
		}

		// Selection produces several types of fragments:
		//   1) wholly outside the selection (fragInfo.IsInsideSelection == false && fragInfo.IsOverlapped == false)
		//   2) wholly inside the selection (fragInfo.IsInsideSelection == true && fragInfo.IsOverlapped == false)
		//   3) where the selection stroke crossed or overlapped the original stroke (fragInfo.IsOverlapped == true)
		//     3.1) and the fragment is considered inside the selection (fragInfo.IsInsideSelection == true)
        //     3.2) and the fragment is considered outside the selection (fragInfo.IsInsideSelection == false)
        //
		// In this sample we keep all 3 fragments as new strokes, with the "overlapped" one included within the selection
		// Depending on your requirements, you can:
		//    Keep all 3 fragments(as in this sample)
		//    Discard the overlapped fragment. 
		//    Reattach the overlapped to fragment either the selected or the non-selected fragment, or even both.
		//      (See SplineFragment.SpliceAdjacentFragments)

		public void ProcessSelectorResult(SelectStrokePartResult result)
        {
            foreach (var fragments in result.Fragments)
            {
                // Selection of raster strokes is not supported, only vector strokes are expected in the result
                Debug.Assert(fragments.Key is VectorStroke);

				Stroke stroke = (Stroke)fragments.Key;
                var spline = stroke.Spline;
                int indexOfSplitStroke = m_controller.ModelFindStrokeIndex(stroke.Id);

                m_controller.ModelRemoveStroke(indexOfSplitStroke);

                foreach (var fragInfo in fragments.Value)
                {
                    SplineFragment frag = fragInfo.Fragment;

                    Spline newSpline = new Spline(stroke.Spline.Path.GetPart(frag.BeginPointIndex, frag.PointsCount), frag.Ts, frag.Tf);

                    Stroke addedStroke = new VectorStroke(
                        Identifier.FromNewGuid(),
                        newSpline,
                        stroke.Color,
                        stroke.Brush,
                        stroke.Size,
                        stroke.Rotation,
                        stroke.ScaleX,
                        stroke.ScaleY,
                        stroke.OffsetX,
                        stroke.OffsetY,
                        stroke.BlendMode);

                    m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke++);

                    addedStroke.RebuildCache();

                    if (fragInfo.IsInsideSelection || fragInfo.IsOverlapped)
                    {
                        m_controller.Selection.Add(addedStroke.Id);
                    }
                }
            }

            m_controller.MeasureSelectedStrokes();
        }
    }
}
