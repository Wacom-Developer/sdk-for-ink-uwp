using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink;
using Windows.Foundation;

namespace WacomInkDemoUWP
{
	class Selection
	{
		private HashSet<Identifier> m_selectedStrokes { get; set; } = new HashSet<Identifier>();

		public Rect BoundingRect { get; set; } = Rect.Empty;

		public void Add(Identifier id)
		{
			m_selectedStrokes.Add(id);
		}

		public void Clear()
		{
			m_selectedStrokes.Clear();

			BoundingRect = Rect.Empty;
		}

		public int Count => m_selectedStrokes.Count;

		public bool Contains(Identifier id)
		{
			return m_selectedStrokes.Contains(id);
		}

		public bool HitTest(Point hitPoint)
		{
			return BoundingRect.Contains(hitPoint);
		}

		public Func<Stroke, bool> GetNotSelectedStrokesFilter()
		{
			Func<Stroke, bool> filter = null;

			if (Count > 0)
			{
				filter = (stroke) => !Contains(stroke.Id);
			}

			return filter;
		}
	}
}
