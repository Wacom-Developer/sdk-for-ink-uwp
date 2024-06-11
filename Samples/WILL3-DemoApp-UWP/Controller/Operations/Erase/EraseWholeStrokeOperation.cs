using System;
using System.Collections.Generic;
using System.Diagnostics;
using Wacom.Ink;
using Wacom.Ink.Manipulations;

namespace WacomInkDemoUWP
{
    class EraseWholeStrokeOperation : EraseStrokeOperation
    {
        #region Constructors

        public EraseWholeStrokeOperation(InkPanelController controller)
            : base(controller)
        {
        }

        #endregion

        #region Interface

        public void PerformEraseOperation(VectorStroke eraserStroke)
        {
            EraseWholeStrokeManipulation eraser = m_controller.CreateEraseWholeStrokeOperation();
            eraser.EraseQuery(eraserStroke);

            ProcessEraserOperationResult(eraser.Result);
        }

        #endregion

        #region DrawStrokeOperation

        protected override void OnStrokeEnd()
        {
            VectorStroke eraserStroke = CreateVectorStroke(
                m_inkBuilder.SplineAccumulator.Accumulated.Clone(),
                Color,
                Tool);

            PerformEraseOperation(eraserStroke);

            // Set redraw flags
            m_controller.ViewInvalidateSceneAndOverlay();

            m_controller.ViewClearCurrentStrokeLayer();

			m_controller.ResetOperation();
		}

        #endregion

        #region Implementation

        private void ProcessEraserOperationResult(HashSet<IInkStroke> eraserResult)
        {
            foreach (var splitStroke in eraserResult)
            {
                Stroke stroke2Delete = (Stroke)splitStroke;

                int indexOfSplitStroke = m_controller.ModelFindStrokeIndex(stroke2Delete.Id);

                if (indexOfSplitStroke == -1)
                {
                    Debug.Assert(false);
                    continue;
                }

                m_controller.ModelRemoveStroke(indexOfSplitStroke);
            }
        }

        #endregion
    }
}
