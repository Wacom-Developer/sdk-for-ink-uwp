using System.Collections.Generic;

namespace WacomInkDemoUWP
{
    public class EraseStrokeOperation : DrawVectorStrokeOperation
    {
        #region Constructors

        public EraseStrokeOperation(InkPanelController controller)
            : base(controller, new List<VectorDrawingTool> { new CircleEraserTool() }, 0)
        {
            m_tag = "eraser";
            m_keepStroke = false;

            Color = Windows.UI.Color.FromArgb(128, 255, 244, 104);
        }

        #endregion

    }
}
