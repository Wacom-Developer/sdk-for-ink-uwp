using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Xaml.Input;

using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;


namespace Wacom
{
    /// <summary>
    /// Base class for Ink Builders
    /// </summary>
    public abstract class InkBuilder
    {
        #region Fields

        /// <summary>
        /// Specifies the properties of each PathPoint used by concrete InkBuilders
        /// </summary>
        public PathPointLayout Layout { get; protected set; }

        /// <summary>
        /// Creates a WILL 3 path from pointer input data
        /// </summary>
        protected PathProducer mPathProducer;

        /// <summary>
        /// Smoothens the path values
        /// </summary>
        protected SmoothingFilter mSmoothingFilter;

        /// <summary>
        /// Converts the path into a Catmull-Rom spline
        /// </summary>
        public SplineProducer SplineProducer { get; protected set; }

        /// <summary>
        /// Storage for path data as it is being accumulated 
        /// </summary>
        protected PathSegment mPathSegment = new PathSegment();

        protected int mPointerDataUpdateCount = 0;

        private bool mCollectPointerData = true;
        private List<PointerData> mPointerDataList = new List<PointerData>();

        #endregion

        #region Public Interface

        /// <summary>
        /// Discretizes the spline to create multiple sampled points along the ink path
        /// </summary>
        public SplineInterpolator SplineInterpolator { get; protected set; }

        /// <summary>
        /// Adds a point to the current path segment
        /// </summary>
        /// <param name="addition">point to add</param>
        public void AddPoint(PointerData addition)
        {
            Phase phase = addition.Phase;

            if (mCollectPointerData)
            {
                if (phase == Phase.Begin)
                { // Clear the pointer data list
                    mPointerDataList.Clear();
                }

                mPointerDataList.Add(addition);
            }

            var geometry = mPathProducer.Add(phase, addition, null);

            mPathSegment.Add(phase, geometry.Addition, geometry.Prediction);
        }

        /// <summary>
        /// Add points from a Pointer event to the current path segment
        /// </summary>
        /// <param name="phase">Phase of input</param>
        /// <param name="uiElement">UI element associated with the pointer event</param>
        /// <param name="args">Pointer event arguments</param>
        public void AddPointsFromEvent(Phase phase, Windows.UI.Xaml.UIElement uiElement, PointerRoutedEventArgs args)
        {
            bool useIntermediatePoints = true;

            if (useIntermediatePoints)
            {
                var intermediate = args.GetIntermediatePoints(uiElement);

                for (int i = intermediate.Count - 1; i >= 0; i--)
                {
                    AddPoint(ConvertPoint(phase, intermediate[i]));
                }
            }
            else
            {
                AddPoint(ConvertPoint(phase, args.GetCurrentPoint(uiElement)));
            }
        }

        /// <summary>
        /// Convert a PointerPoint to a PointerData
        /// </summary>
        /// <param name="phase">Phase of input</param>
        /// <param name="pp">PointerPoint to convert</param>
        public static PointerData ConvertPoint(Phase phase, PointerPoint pp)
        {
            float altitude;
            float azimuth;

            PointerData.CalculateAltitudeAndAzimuth(pp.Properties.XTilt, pp.Properties.YTilt, out altitude, out azimuth);

            var pointerData = new PointerData((float)pp.Position.X, (float)pp.Position.Y, phase, (long)pp.Timestamp);
            pointerData.Force = pp.Properties.Pressure;
            pointerData.AltitudeAngle = altitude;
            pointerData.AzimuthAngle = azimuth;

            return pointerData;
        }

        public List<PointerData> GetPointerDataList()
        {
            if (!mCollectPointerData)
                throw new Exception("InkBuilder is not constructed to collect pointer data.");

            return new List<PointerData>(mPointerDataList);
        }

        #endregion
    }
}
