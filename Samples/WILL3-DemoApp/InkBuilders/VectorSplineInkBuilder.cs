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
		private ConvexHullChainProducer m_convexHullChainProducer = new ConvexHullChainProducer();
		private PolygonMerger m_polygonMerger = new PolygonMerger();
		private PolygonSimplifier m_polygonSimplifier = new PolygonSimplifier(0.1f);

		public PipelineData AddWholePath(Spline path, PathPointLayout layout, Wacom.Ink.Geometry.VectorBrush vectorBrush)
		{
			var splineInterpolator = new CurvatureBasedInterpolator(layout);
			var brushApplier = new BrushApplier(layout, vectorBrush);

			var points = splineInterpolator.Add(true, true, path, null);

			var polys = brushApplier.Add(true, true, points.Addition, points.Prediction);

			var hulls = m_convexHullChainProducer.Add(true, true, polys.Addition, polys.Prediction);

			var merged = m_polygonMerger.Add(true, true, hulls.Addition, hulls.Prediction);

			return new PipelineData(polys, merged);
		}
	}
}
