using System;
using System.Collections.Generic;
using System.Diagnostics;
using Wacom.Ink;
using Wacom.Ink.Manipulations;

namespace WacomInkDemoUWP
{
    public class EraseWholeStrokeOperation : EraseStrokeOperation
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

            // FIX: modifying the Strokes collection from this thread is dangerous
            ProcessEraserOperationResult(eraser.Result);

            m_controller.ModelEnsureStrokesCacheExists();
        }


        #endregion

        #region DrawStrokeOperation

        protected override void OnStrokeEnd()
        {
            VectorStroke eraserStroke = m_controller.ModelCreateVectorStroke(
                m_inkBuilder.SplineAccumulator.Accumulated.Clone(),
                Color,
                Tool,
                m_tag);

            //m_controller.Replayer.EnqueueCommand(new DrawStrokeCommand(eraserStroke, m_controller));
            //m_controller.Replayer.EnqueueCommand(new PerformEraseCommand(eraserStroke, m_controller));

            PerformEraseOperation(eraserStroke);

            // Set redraw flags
            m_controller.ViewInvalidateSceneAndOverlay();

            m_controller.ViewClearCurrentStrokeLayer();

            // Store the stroke in the collection
            if (m_keepStroke)
            {
                m_controller.ModelStoreStroke(eraserStroke);
            }
        }

        #endregion

        #region Implementation

        private void ProcessEraserOperationResult(HashSet<IInkStroke> eraserResult)
        {
            foreach (var splitStroke in eraserResult)
            {
                Stroke stroke2Delete = (Stroke)splitStroke;

                if (stroke2Delete.Tag == "eraser")
                    throw new Exception("WHAAT?");

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
