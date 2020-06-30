
namespace Wacom
{
    /// <summary>
    /// Keeps a record of which pointer device (pen, mouse etc) ink collection is currently associated with 
    /// to avoid potential conflicts should 2 or more pointer devices be in use simultaneously
    /// </summary>
    public struct PointerManager
    {
        #region Fields

        private uint? mPointerId;

        #endregion


        public bool OnPressed(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (mPointerId.HasValue)
            {
                return false;
            }

            mPointerId = args.Pointer.PointerId;

            return true;
        }

        public bool OnMoved(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // Accept only the saved pointer, reject others
            return mPointerId.HasValue && (args.Pointer.PointerId == mPointerId.Value);
        }

        public bool OnReleased(Windows.UI.Xaml.Input.PointerRoutedEventArgs args)
        {
            // Reject events from other pointers
            if (!mPointerId.HasValue || (args.Pointer.PointerId != mPointerId.Value))
            {
                return false;
            }

            mPointerId = null;

            return true;
        }

    }
}
