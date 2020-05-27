using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Manipulation;

namespace Tutorial_03
{
	struct Interval
	{
		public int FromIndex;
		public int ToIndex;
		public float FromTValue;
		public float ToTValue;
		public bool Inside;

		public int GetSize()
		{
			return ToIndex - FromIndex + 1;
		}
	}

	class IntervalList
	{
		private IntersectionResult _result;
		private int _count;

		public IntervalList(IntersectionResult result)
		{
			_result = result;
			_count = _result.Inside.Count;

			Debug.Assert(_count * 2 == _result.Indices.Count);
			Debug.Assert(_result.Indices.Count == _result.TValues.Count);
		}

		public int Count
		{
			get
			{
				return _count;
			}
		}
		public Interval this[int index]
		{
			get
			{
				int index1 = index * 2;
				int index2 = index1 + 1;

				Interval interval;
				interval.FromIndex = (int)_result.Indices[index1];
				interval.ToIndex = (int)_result.Indices[index2];
				interval.FromTValue = _result.TValues[index1];
				interval.ToTValue = _result.TValues[index2];
				interval.Inside = _result.Inside[index] == 1;

				return interval;
			}
		}
	}
}
