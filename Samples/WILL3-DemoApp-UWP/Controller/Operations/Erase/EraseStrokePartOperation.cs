using System;
using System.Diagnostics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;

namespace WacomInkDemoUWP
{
    class EraseStrokePartOperation : EraseStrokeOperation
    {
        #region Constructors

        public EraseStrokePartOperation(InkPanelController controller)
            : base(controller)
        {
        }

        #endregion

        #region Interface

        public void PerformEraseOperation(VectorStroke eraserStroke)
        {
            EraseStrokePartManipulation eraser = m_controller.CreateEraseStrokePartOperation();
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

        private void ProcessEraserOperationResult(EraseStrokePartResult eraserResult)
        {
            foreach (var splitStroke in eraserResult.StrokeFragments)
            {
                Stroke stroke2Delete = (Stroke)splitStroke.Key;

                int indexOfSplitStroke = m_controller.ModelFindStrokeIndex(stroke2Delete.Id);

                if (indexOfSplitStroke == -1)
                {
                    Debug.Assert(false);
                    continue;
                }

                m_controller.ModelRemoveStroke(indexOfSplitStroke);

                foreach (var fragment in splitStroke.Value)
                {
                    Spline newSpline = new Spline(stroke2Delete.Spline.Path.GetPart(fragment.BeginPointIndex, fragment.PointsCount), fragment.Ts, fragment.Tf);

                    if (stroke2Delete is VectorStroke)
                    {
                        VectorStroke addedStroke = new VectorStroke(
                            Identifier.FromNewGuid(),
                            newSpline,
                            stroke2Delete.Color,
                            stroke2Delete.Brush,
                            stroke2Delete.Size,
                            stroke2Delete.Rotation,
                            stroke2Delete.ScaleX,
                            stroke2Delete.ScaleY,
                            stroke2Delete.OffsetX,
                            stroke2Delete.OffsetY,
                            stroke2Delete.BlendMode);

                        m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke);
                    }
                    else if (stroke2Delete is RasterStroke rasterStroke2Delete)
                    {
                        // Erasing of raster strokes is currently not supported.
                        Debug.Assert(false, "Raster strokes should not be present in the eraser result!");

                        RasterStroke addedStroke = new RasterStroke(
                            Identifier.FromNewGuid(),
                            newSpline,
                            stroke2Delete.Color,
                            stroke2Delete.Brush,
                            stroke2Delete.Size,
                            stroke2Delete.Rotation,
                            stroke2Delete.ScaleX,
                            stroke2Delete.ScaleY,
                            stroke2Delete.OffsetX,
                            stroke2Delete.OffsetY,
                            stroke2Delete.BlendMode,
                            rasterStroke2Delete.RandomSeed);

						m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke);
                    }
                }
            }
        }

        #endregion
    }
}
