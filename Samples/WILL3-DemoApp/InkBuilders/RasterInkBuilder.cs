using System;
using System.Collections.Generic;
using Windows.Foundation;

using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

namespace Wacom
{
    /// <summary>
    /// Manages ink geometry pipeline for raster (particle) brushes.
    /// </summary>
    public class RasterInkBuilder : InkBuilder
    {
        #region Fields

        private const float defaultSpacing = 0.15f;
        private const int splitCount = 1;

        #endregion

        #region Constructors

        public RasterInkBuilder()
        {
        }

        #endregion

        #region Properties

        public event EventHandler LayoutUpdated;

        #endregion

        /// <summary>
        /// Transform accumulated pointer input to ink geometry
        /// </summary>
        /// <returns>Tuple containing added data (Item1) and predicted or preliminary data (Item2)</returns>
        /// <remarks>Passes accumulated path segment (from PathProducer) through remaining stages of 
        /// the raster ink pipeline - Smoother, SplineProducer & SplineInterpolator</remarks>
        public ProcessorResult<List<float>> GetPath()
        {
            var smoothPath = mSmoothingFilter.Add(mPathSegment.IsFirst, mPathSegment.IsLast, mPathSegment.AccumulatedAddition, mPathSegment.LastPrediction);

            var spline = SplineProducer.Add(mPathSegment.IsFirst, mPathSegment.IsLast, smoothPath.Addition, smoothPath.Prediction);

            var points = SplineInterpolator.Add(mPathSegment.IsFirst, mPathSegment.IsLast, spline.Addition, spline.Prediction);

            mPathSegment.Reset();
            mPointerDataUpdateCount = 0;

            return points;
        }

        #region Public Interface

        public void UpdatePipeline(PathPointLayout layout, Calculator calculator, float spacing)
        {
            bool layoutChanged = false;
            bool otherChange = false;

            if ((Layout == null) || (layout.ChannelMask != Layout.ChannelMask))
            {
                Layout = layout;
                layoutChanged = true;
            }

            if (mPathProducer == null || calculator != mPathProducer.PathPointCalculator || layoutChanged)
            {
                mPathProducer = new PathProducer(Layout, calculator, true);
                otherChange = true;
            }

            if (mSmoothingFilter == null || layoutChanged)
            {
                mSmoothingFilter = new SmoothingFilter(Layout.Count)
                {
                    KeepAllData = true
                };
                otherChange = true;
            }

            if (SplineProducer == null || layoutChanged)
            {
                SplineProducer = new SplineProducer(Layout, true);
                otherChange = true;
            }

            if (SplineInterpolator == null || layoutChanged)
            {
                SplineInterpolator = new DistanceBasedInterpolator(Layout, spacing, splitCount, true, true, true);
                otherChange = true;
            }
            ((DistanceBasedInterpolator) SplineInterpolator).Spacing = spacing;

            if (layoutChanged || otherChange)
            {
                LayoutUpdated?.Invoke(this, EventArgs.Empty);
            }
        }       

        #endregion
    }
}
