
namespace Wacom
{
    /// <summary>
    /// Keeps a record of which pointer device (pen, mouse etc) ink collection is currently associated with 
    /// to avoid potential conflicts should 2 or more pointer devices be in use simultaneously
    /// </summary>
    public struct PointerManager
    {
        #region Fields

        private uint? m_pointerId;

        #endregion


        public bool OnPressed(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (m_pointerId.HasValue)
            {
                return false;
            }

            m_pointerId = args.Pointer.PointerId;

            return true;
        }

        public bool OnMoved(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // Accept only the saved pointer, reject others
            return m_pointerId.HasValue && (args.Pointer.PointerId == m_pointerId.Value);
        }

        public bool OnReleased(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // Reject events from other pointers
            if (!m_pointerId.HasValue || (args.Pointer.PointerId != m_pointerId.Value))
            {
                return false;
            }

            m_pointerId = null;

            return true;
        }

    }
}
