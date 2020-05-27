using System.Collections.Generic;
using Wacom.Ink;
using Wacom.Ink.Serialization;
using Windows.Foundation;
using Windows.UI;

namespace Tutorial_03
{
	internal class Stroke
	{
		#region Fields

		// bounds cache
		private List<Rect> _cachedSegmentsBounds;
		private Rect? _cachedBounds;

		#endregion

		#region Constructors

		public Stroke(StrokeData strokeData)
		{
			Path = strokeData.Path;

			Width = strokeData.Width;
			Color = strokeData.Color;
			Ts = strokeData.Ts;
			Tf = strokeData.Tf;
		}
		public Stroke(Stroke srcStroke, Interval interval)
		{
			Path = srcStroke.Path.ClonePathPart(interval.FromIndex, interval.GetSize());

			Width = srcStroke.Width;
			Color = srcStroke.Color;
			Ts = interval.FromTValue;
			Tf = interval.ToTValue;
		}

		#endregion

		#region Methods

		public List<Rect> GetBounds(out Rect bounds)
		{
			if (!_cachedBounds.HasValue)
			{
				Rect tmpBounds;
				List<Rect> tmpSegmentsBounds;

				if ((Path != null) && (Path.PointsCount > 0))
				{
					int segmentsCount = Path.SegmentsCount;

					if (segmentsCount > 0)
					{
						tmpBounds = Path.CalculateSegmentBounds(0, this.Width, 0.0f);
						tmpSegmentsBounds = new List<Rect>();
						tmpSegmentsBounds.Add(tmpBounds);

						for (int i = 1; i < segmentsCount; i++)
						{
							Rect rc = Path.CalculateSegmentBounds(i, this.Width, 0.0f);
							tmpBounds.Union(rc);
							tmpSegmentsBounds.Add(rc);
						}

						_cachedBounds = tmpBounds;
						_cachedSegmentsBounds = tmpSegmentsBounds;
					}
				}
			}

			if (!_cachedBounds.HasValue)
				return null;

			bounds = _cachedBounds.Value;
			return _cachedSegmentsBounds;
		}

		#endregion

		#region Properties

		public Path Path { get; private set; }
		public float? Width { get; set; }
		public Color Color { get; set; }
		public float Ts { get; set; }
		public float Tf { get; set; }

		#endregion
	}
}
