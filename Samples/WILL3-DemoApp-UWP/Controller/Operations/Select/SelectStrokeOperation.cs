using System.Collections.Generic;
using System.Numerics;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public abstract class SelectStrokeOperation : DrawVectorStrokeOperation
    {
        protected List<Vector2> m_selectorPath;

        #region Constructors

        public SelectStrokeOperation(InkPanelController controller)
            : base(controller, new List<VectorDrawingTool> { new CircleSelectorTool() }, 0)
        {
            Color = Windows.UI.Color.FromArgb(128, 196, 0, 0);
        }

        #endregion

        public override void OnPointerPressed(PointerEventArgs args)
        {
            m_selectorPath = new List<Vector2>();

            AddPointToSelectorPath(args.CurrentPoint);

            base.OnPointerPressed(args);
        }

        public override void OnPointerMoved(PointerEventArgs args)
        {
            AddPointToSelectorPath(args.CurrentPoint);
            base.OnPointerMoved(args);
        }

        protected void AddPointToSelectorPath(Windows.UI.Input.PointerPoint pointerPoint)
        {
            Vector2 pp = pointerPoint.Position.ToVector2();
            m_selectorPath.Add(m_controller.ViewTransformToModel(pp));
        }
    }
}
