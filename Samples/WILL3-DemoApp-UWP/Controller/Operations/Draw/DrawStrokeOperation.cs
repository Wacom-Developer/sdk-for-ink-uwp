using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    abstract class DrawStrokeOperation : UserOperation
    {
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
