using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Input;

using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace Wacom
{
    using PolygonVertices = List<Vector2>;

    /// <summary>
    /// Manages ink geometry pipeline for vector brushes.
    /// </summary>
    public class VectorInkBuilder : InkBuilder
    {
        #region Fields

        private readonly VectorBrush mBrush;


        private PolygonMerger mPolygonMerger;

        #endregion

        #region Properties

        public PolygonSimplifier PolygonSimplifier { get; private set; }
        public ConvexHullChainProducer ConvexHullChainProducer { get; private set; }

        public BrushApplier BrushApplier { get; private set; }

        #endregion

        #region Constructors

        public VectorInkBuilder()
        {
            BrushPolygon bp4 = new BrushPolygon(0.0f, VectorBrushFactory.CreateEllipseBrush(4, 1.0f, 1.0f));
            BrushPolygon bp8 = new BrushPolygon(2.0f, VectorBrushFactory.CreateEllipseBrush(8, 1.0f, 1.0f));
            BrushPolygon bp16 = new BrushPolygon(6.0f, VectorBrushFactory.CreateEllipseBrush(16, 1.0f, 1.0f));
            BrushPolygon bp32 = new BrushPolygon(18.0f, VectorBrushFactory.CreateEllipseBrush(32, 1.0f, 1.0f));
            mBrush = new VectorBrush(bp4, bp8, bp16, bp32);
        }

        #endregion

        #region Public Interface


        /// <summary>
        /// Transform accumulated pointer input to ink geometry
        /// </summary>
        /// <param name="defaultSize">Default size for SplineInterpolator and BrushApplier</param>
        /// <param name="defaultScale">Default Scale for BrushApplier</param>
        /// <param name="defaultOffset">Default Offset for BrushApplier</param>
        /// <param name="defaultRotation">Default Rotation for BrushApplier</param>
        /// <returns>Tuple containing added data (Item1) and predicted or preliminary data (Item2)</returns>
        /// <remarks>Passes accumulated path segment (from PathProducer) through remaining stages of 
        /// the vector ink pipeline - Smoother, SplineProducer, SplineInterpolator, BrushApplier, ConvexHullChainProducer,
        /// PolygonMerger and PolygonSimplifier</remarks>
        public ProcessorResult<List<PolygonVertices>> GetPolygons()
        {
            var smoothPath = mSmoothingFilter.Add(mPathSegment.IsFirst, mPathSegment.IsLast, mPathSegment.AccumulatedAddition, mPathSegment.LastPrediction);

            var spline = SplineProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, smoothPath.Addition, smoothPath.Prediction);

            var points = SplineInterpolator.Add(mPathSegment.IsFirst, mPathSegment.IsLast, spline.Addition, spline.Prediction);

            var polys = BrushApplier.Add(mPathSegment.IsFirst, mPathSegment.IsLast, points.Addition, points.Prediction);

            var hulls = ConvexHullChainProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, polys.Addition, polys.Prediction);

            var merged = mPolygonMerger.Add(mPathSegment.IsFirst, mPathSegment.IsLast, hulls.Addition, hulls.Prediction);

            var simplified = PolygonSimplifier.Add(mPathSegment.IsFirst, mPathSegment.IsLast, merged.Addition, merged.Prediction);

            mPathSegment.Reset();

            return simplified;
        }

        #endregion

        #region Calculators

        public void UpdatePipeline(PathPointLayout layout, Calculator calculator, VectorBrush brush)
        {
            bool layoutChanged = false;

            if ((Layout == null) || (layout.ChannelMask != Layout.ChannelMask))
            {
                Layout = layout;
                layoutChanged = true;
            }

            if (mPathProducer == null || calculator != mPathProducer.PathPointCalculator || layoutChanged)
            {
                mPathProducer = new PathProducer(Layout, calculator)
                {
                    KeepAllData = true
                };
            }

            if (mSmoothingFilter == null || layoutChanged)
            {
                mSmoothingFilter = new SmoothingFilter(Layout.Count)
                {
                    KeepAllData = true
                };
            }

            if (SplineProducer == null || layoutChanged)
            {
                SplineProducer = new SplineProducer(Layout)
                {
                    KeepAllData = true
                };
            }

            if (SplineInterpolator == null || layoutChanged)
            {
                SplineInterpolator = new CurvatureBasedInterpolator(Layout)
                {
                    KeepAllData = true
                };
            }

            if (BrushApplier == null || (brush != BrushApplier.Prototype) || layoutChanged)
            {
                BrushApplier = new BrushApplier(Layout, brush)
                {
                    KeepAllData = true
                };
            }

            if (ConvexHullChainProducer == null)
            {
                ConvexHullChainProducer = new ConvexHullChainProducer()
                {
                    KeepAllData = true
                };

            }

            if (mPolygonMerger == null)
            {
                mPolygonMerger = new PolygonMerger()
                {
                    KeepAllData = true
                };

            }

            if (PolygonSimplifier == null)
            {
                PolygonSimplifier = new PolygonSimplifier(0.1f)
                {
                    KeepAllData = true
                };

            }
        }

        #endregion

    }
}
