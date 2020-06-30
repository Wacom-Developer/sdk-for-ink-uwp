using System.Collections.Generic;
using Wacom.Ink;
using Wacom.Ink.Geometry;

namespace Wacom
{
	public class VectorInkStrokeFactory : IInkStrokeFactory
	{
		public IInkStroke CreateStroke(Spline newSpline, IInkStroke originalStroke, int firstPointIndex, int pointsCount)
		{
			VectorInkStroke stroke = new VectorInkStroke(newSpline, originalStroke, firstPointIndex, pointsCount);

			return stroke;
		}
	}
}
