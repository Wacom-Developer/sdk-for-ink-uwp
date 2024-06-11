using System.Collections.Generic;

namespace WacomInkDemoUWP
{
    class EraseStrokeOperation : DrawVectorStrokeOperation
    {
        #region Constructors

        public EraseStrokeOperation(InkPanelController controller)
            : base(controller, new List<VectorDrawingTool> { new CircleEraserTool() }, 0)
        {
            Color = Windows.UI.Color.FromArgb(128, 255, 244, 104);
        }

        #endregion
    }
}
