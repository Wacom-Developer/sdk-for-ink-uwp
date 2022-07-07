using System.Linq;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public enum CancellationReason
    {
        Manual,
        FocusLost
    }

    public class UserOperation
    {
        #region Fields

        protected InkPanelController m_controller = null;

        #endregion

        #region Constructors

        public UserOperation(InkPanelController controller)
        {
            m_controller = controller;
        }

        #endregion

        #region Properties

        public Windows.UI.Color Color { get; set; }

        #endregion

        #region Interface

        public virtual void OnPointerPressed(PointerEventArgs args)
        {
        }

        public virtual void OnPointerMoved(PointerEventArgs args)
        {
        }

        public virtual void OnPointerReleased(PointerEventArgs args)
        {
        }

        public virtual void OnPointerWheelChanged(PointerEventArgs args)
        {
        }

        public virtual void OnCanceled(CancellationReason reason)
        {
        }

        public virtual void UpdateView(InkPanelModel model, InkPanelView view)
        {
            view.TryResize();
            view.TryRedrawAllStrokes(model.Strokes.Where(stroke => !model.SelectedStrokes.Contains(stroke.Id)));
            view.TryOverlayRedraw(model);
        }

        #endregion
    }
}
