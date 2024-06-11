using System.Numerics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI;

using UimPathPointProperties = Wacom.Ink.Serialization.Model.PathPointProperties;

namespace WacomInkDemoUWP
{
	class RasterStroke : Stroke
	{
		#region Constructors

		public RasterStroke(
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
			BlendMode blendMode,
			uint randomSeed) :
			base(id, spline, color, brush, size, rotation, scaleX, scaleY, offsetX, offsetY, blendMode)
		{
			RandomSeed = randomSeed;
		}

		public RasterStroke(
			Identifier id,
			Spline spline,
			AppBrush brush,
			UimPathPointProperties props,
			BlendMode blendMode,
			uint randomSeed) :
			base(id, spline, brush, props, blendMode)
		{
			RandomSeed = randomSeed;
		}

		#endregion

		#region Properties

		public uint RandomSeed { get; private set; }

		public float ParticleSpacing => Brush.Spacing;

		public ParticleList Particles { get; } = new ParticleList();

		#endregion

		#region Overrides from Stroke

		public override void RebuildCache()
		{
			DistanceBasedInterpolator interpolator = new DistanceBasedInterpolator(
				spacing: Brush.Spacing,
				splitCount: 6,
				interpolateByLength: true,
				calculateTangents: true,
				keepAllData: false)
			{
				DefaultSize = Size,
				AccumulateSplineParameters = true
			};

			BrushApplier brushApplier = new BrushApplier(VectorBrush);
			brushApplier.Prototype = Brush.VectorBrush;
			brushApplier.DefaultSize = Size;
			brushApplier.DefaultRotation = Rotation;
			brushApplier.DefaultScale = new Vector3(ScaleX, ScaleY, 1.0f);
			brushApplier.DefaultOffset = new Vector3(OffsetX, OffsetY, 0.0f);

			brushApplier.SetDataProvider(interpolator);

			interpolator.Process(true, true, Spline, null);
			brushApplier.Process();

			Particles.Assign(interpolator.Addition, (uint)interpolator.Addition.LayoutMask);
		}

		public override void ClearCache()
		{
			Particles.RemoveAll();
		}

		public override bool HasCache()
		{
			return !Particles.IsEmpty;
		}

		#endregion
	}
}
