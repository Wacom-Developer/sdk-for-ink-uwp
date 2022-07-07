using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public abstract class DrawStrokeOperation : UserOperation
    {
        #region Fields

        protected string m_tag = null;
        protected bool m_keepStroke = true;
        protected bool m_ended = false;

        #endregion

        #region Properties

        //public bool UseRandomInkColor { get; set; }

        #endregion

        #region Constructors

        public DrawStrokeOperation(InkPanelController controller) : base(controller)
        {
        }

        #endregion

        #region Implementation

        protected abstract void SetupInkTool(PointerEventArgs args);

        protected abstract void OnStrokeEnd();

        #endregion
    }
}
