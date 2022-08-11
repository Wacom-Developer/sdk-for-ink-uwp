using System;
using System.Diagnostics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;

namespace WacomInkDemoUWP
{
    public class EraseStrokePartOperation : EraseStrokeOperation
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

        private void ProcessEraserOperationResult(EraseStrokePartResult eraserResult)
        {
            bool drawIntervals = false;

            foreach (var splitStroke in eraserResult.StrokeFragments)
            {
                Stroke stroke2Delete = (Stroke)splitStroke.Key;

                if (stroke2Delete.Tag == "eraser")
                    throw new Exception("WHAAT?");

                var fragments = splitStroke.Value;

                if (drawIntervals)
                {
                    foreach (var frag in fragments)
                    {
                        //var sb = sampler.GetShapeForSplintPoint(interval.m_begin, Colors.Green);
                        //var se = sampler.GetShapeForSplintPoint(interval.m_end, Colors.Red);
                        //m_model.CustomShapes.Add(sb);
                        //m_model.CustomShapes.Add(se);
                    }
                }

                int originalPointsCount = stroke2Delete.GetSplineControlPointsCount();

                int indexOfSplitStroke = m_controller.ModelFindStrokeIndex(stroke2Delete.Id);

                if (indexOfSplitStroke == -1)
                {
                    Debug.Assert(false);
                    continue;
                }

                m_controller.ModelRemoveStroke(indexOfSplitStroke);

                foreach (var frag in fragments)
                {
                    Spline newSpline = new Spline(stroke2Delete.Spline.Path.GetPart(frag.BeginPointIndex, frag.PointsCount), frag.Ts, frag.Tf);

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
                            stroke2Delete.ViewToModelScale,
                            stroke2Delete.BlendMode,
                            0,
                            stroke2Delete.Tag);

                        m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke);
                    }
                    else if (stroke2Delete is RasterStroke rasterStroke2Delete)
                    {
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
                            stroke2Delete.ViewToModelScale,
                            stroke2Delete.BlendMode,
                            rasterStroke2Delete.RandomSeed, // FIX: must recalculate random seed
                            0,
                            stroke2Delete.Tag);

                        m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke);
                    }
                }
            }
        }

        #endregion
    }
}
