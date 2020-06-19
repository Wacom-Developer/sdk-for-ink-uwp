using System;
using Windows.Foundation;

namespace Wacom
{
    /// <summary>
    /// Manages the area requiring updating as ink is collected 
    /// </summary>
	public class DirtyRectManager
	{
		private Rect m_prevPredictedStrokeRect;

		public DirtyRectManager()
		{
			m_prevPredictedStrokeRect = Rect.Empty;
		}

		/// <summary>
		/// Call this before UpdatePreliminaryPathRect if you want the result Rect to be calculated based on the previous preliminary path rect.
		/// </summary>
		/// <param name="currentChunkRect"></param>
		/// <returns>The union of the specified rect and the rect stored in this object.</returns>
		public Rect GetUnionRect(Rect currentChunkRect)
		{
			currentChunkRect.Union(m_prevPredictedStrokeRect);

			return currentChunkRect;
		}

		/// <summary>
		/// Calculates the update rect for the current frame.
		/// </summary>
		/// <param name="addedStrokeRect"></param>
		/// <param name="predictedStrokeRect"></param>
		/// <remarks>
		/// The update rect for the current frame is the union of the following rectangles:
		/// 1. Bounding rect of the predicted stroke from previous frame. (This preliminary stroke from previous frame has to be overwriten).
		/// 2. Bounding rect of the added stroke from current frame.
		/// 3. Bounding rect of the predicted stroke rect from current frame.
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
