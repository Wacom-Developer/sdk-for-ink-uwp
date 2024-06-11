using Windows.Foundation;

namespace WacomInkDemoUWP
{
    class DirtyRectManager
    {
        private Rect m_prevPredictedStrokeRect;

        public DirtyRectManager()
        {
            m_prevPredictedStrokeRect = Rect.Empty;
        }

        /// <summary>
        /// Calculates the update rectangle for the current frame.
        /// </summary>
        /// <param name="addedStrokeRect"></param>
        /// <param name="predictedStrokeRect"></param>
        /// <remarks>
        /// The update rectangle for the current frame is the union of the following rectangles:
        /// 1. Bounding rectangle of the predicted stroke from previous frame. (This preliminary stroke from previous frame has to be overwritten).
        /// 2. Bounding rectangle of the added stroke from current frame.
        /// 3. Bounding rectangle of the predicted stroke rectangle from current frame.
        /// </remarks>
        /// <returns></returns>
        public Rect GetUpdateRect(Rect addedStrokeRect, Rect predictedStrokeRect)
        {
            Rect updateRect = addedStrokeRect;

            if (!m_prevPredictedStrokeRect.IsEmpty)
            {
                updateRect.Union(m_prevPredictedStrokeRect);
            }

            if (!predictedStrokeRect.IsEmpty)
            {
                updateRect.Union(predictedStrokeRect);
            }

            m_prevPredictedStrokeRect = predictedStrokeRect;

            return updateRect;
        }

        /// <summary>
        /// Reset the previous preliminary path on END phase after finished using the manager.
        /// </summary>
        public void Reset()
        {
            m_prevPredictedStrokeRect = Rect.Empty;
        }
    }
}
