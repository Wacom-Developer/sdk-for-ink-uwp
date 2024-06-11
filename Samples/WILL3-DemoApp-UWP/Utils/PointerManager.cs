
namespace WacomInkDemoUWP
{
    struct PointerManager
    {
        #region Fields

        private uint? m_pointerId;

        #endregion

        #region PointerEventArgs

        public bool OnPressed(Windows.UI.Core.PointerEventArgs args)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (m_pointerId.HasValue)
            {
                return false;
            }

            m_pointerId = args.CurrentPoint.PointerId;

            return true;
        }

        public bool OnMoved(Windows.UI.Core.PointerEventArgs args)
        {
            // Accept only the saved pointer, reject others
            return m_pointerId.HasValue && (args.CurrentPoint.PointerId == m_pointerId.Value);
        }

        public bool OnReleased(Windows.UI.Core.PointerEventArgs args)
        {
            // Reject events from other pointers
            if (!m_pointerId.HasValue || (args.CurrentPoint.PointerId != m_pointerId.Value))
            {
                return false;
            }

            m_pointerId = null;

            return true;
        }

        #endregion
    }
}
