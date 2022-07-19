using System.Linq;
using System.Collections.Generic;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;
using Windows.UI;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public class SelectStrokePartOperation : SelectStrokeOperation
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

            m_selectorPath = null;

            // Set redraw flags
            m_controller.ViewInvalidateSceneAndOverlay();

            m_controller.ViewClearCurrentStrokeLayer();
        }

        public void PerformSelectOperation(SelectionContours selection)
        {
            //m_controller.ModelClearCustomShapes();

            SelectStrokePartManipulation selector = m_controller.CreateSelectStrokePartOperation();

            selector.SelectQuery(selection);

            ProcessSelectorResult(selector.Result);

            m_controller.ModelEnsureStrokesCacheExists();
        }


        public void ProcessSelectorResult(SelectStrokePartResult result)
        {
            // Translation (moving) raster strokes is not supported so only select vector strokes
            foreach (var fragments in result.Fragments.Where(f => f.Key is VectorStroke))
            {
                Stroke stroke = (Stroke)fragments.Key;
                var spline = stroke.Spline;
                int indexOfSplitStroke = m_controller.ModelFindStrokeIndex(stroke.Id);

                m_controller.ModelRemoveStroke(indexOfSplitStroke);

                foreach (var fragInfo in fragments.Value)
                {
                    // Overlapped fragments represent the areas where the selection stroke crossed over an ink stroke
                    // To discard those fragments, rather than include them, uncomment the following line
                    //if (!fragInfo.IsOverlapped)
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
                            stroke.ViewToModelScale,
                            stroke.BlendMode,
                            0,
                            stroke.Tag);

                        m_controller.ModelStoreStroke(addedStroke, indexOfSplitStroke++);
                        addedStroke.RebuildCache(true);
                        if (fragInfo.IsInsideSelection || fragInfo.IsOverlapped)
                        {
                            m_controller.ModelSelectStroke(addedStroke.Id);
                        }
                    }
                }
            }
            m_controller.MeasureSelectedStrokes();
        }
    }
}
