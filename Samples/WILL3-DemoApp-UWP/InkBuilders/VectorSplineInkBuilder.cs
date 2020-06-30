using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;

namespace Wacom
{
	/// <summary>
	/// This ink biulder works with splines in model coordinates.
	/// </summary>
	public class VectorSplineInkBuilder
	{
		private ConvexHullChainProducer mConvexHullChainProducer = new ConvexHullChainProducer();
		private PolygonMerger mPolygonMerger = new PolygonMerger();
		private PolygonSimplifier mPolygonSimplifier = new PolygonSimplifier(0.1f);

		public PipelineData AddWholePath(Spline path, PathPointLayout layout, Wacom.Ink.Geometry.VectorBrush vectorBrush)
		{
			var splineInterpolator = new CurvatureBasedInterpolator(layout);
			var brushApplier = new BrushApplier(layout, vectorBrush);

			var points = splineInterpolator.Add(true, true, path, null);

			var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);

			var hulls = mConvexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);

			var merged = mPolygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);

			return new PipelineData(polys, merged);
		}
	}
}
