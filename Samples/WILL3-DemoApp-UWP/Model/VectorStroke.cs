using System.Numerics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI;

using UimPathPointProperties = Wacom.Ink.Serialization.Model.PathPointProperties;

namespace WacomInkDemoUWP
{
	class VectorStroke : Stroke
	{
		#region Fields

		// Cache
		public Polygon m_polygon;

		#endregion

		#region Constructors

		public VectorStroke(
			Identifier id,
			Spline spline,
			Color color,
			AppBrush brush,
			float size,
			float rotation,
			float scaleX,
			float scaleY,
			float offsetX,
			float offsetY,
			BlendMode blendMode) :
			base(id, spline, color, brush, size, rotation, scaleX, scaleY, offsetX, offsetY, blendMode)
		{
		}

		public VectorStroke(
			Identifier id,
			Spline spline,
			AppBrush brush,
			UimPathPointProperties props,
			BlendMode blendMode) :
			base(id, spline, brush, props, blendMode)
		{
		}

		#endregion

		#region Stroke API

		public override void RebuildCache()
		{
			CurvatureBasedInterpolator interpolator = new CurvatureBasedInterpolator();
			BrushApplier brushApplier = new BrushApplier();
			ConvexHullChainProducer hullProducer = new ConvexHullChainProducer();
			PolygonMerger merger = new PolygonMerger();

			interpolator.AccumulateSplineParameters = false;
			interpolator.Process(true, true, Spline, null);
			
			brushApplier.Prototype = VectorBrush;
			brushApplier.DefaultSize = Size;
			brushApplier.DefaultRotation = Rotation;
			brushApplier.DefaultScale = new Vector3(ScaleX, ScaleY, 1.0f);
			brushApplier.DefaultOffset = new Vector3(OffsetX, OffsetY, 0.0f);
			brushApplier.Process(true, true, interpolator.Addition, null);
			
			hullProducer.Process(true, true, brushApplier.Addition, null);
			
			merger.Process(true, true, hullProducer.Addition, null);

			m_polygon = merger.Addition.ToPolygon();
		}

		public override void ClearCache()
		{
			m_polygon = null;
		}

		public override bool HasCache()
		{
			return m_polygon != null;
		}

		#endregion
	}
}
