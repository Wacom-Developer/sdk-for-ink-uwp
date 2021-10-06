using System;
using System.Collections.Generic;
using Wacom;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace Wacom
{
	public class VectorInkBuilder : StockVectorInkBuilder
	{
		SensorDataAccumulator SensorDataAccumulator { get; set; }

		public VectorInkBuilder(bool storeSensorData)
		{
			if (storeSensorData)
			{
				SensorDataAccumulator = new SensorDataAccumulator();
				SensorDataAccumulator.SetDataProvider(PointerDataProvider);
			}

			ConvexHullChainProducer.KeepAllData = true;
		}

		public void UpdatePipeline(LayoutMask layoutMask, Calculator calculator, VectorBrush brush)
		{
			PathProducer.LayoutMask = layoutMask;
			PathProducer.PathPointCalculator = calculator;
			BrushApplier.Prototype = brush;
		}

		public void AddPointsFromEvent(Phase phase, PointerEventArgs args)
		{
			PointerDataProvider.AddPointsFromEvent(phase, args);

			if (SensorDataAccumulator != null)
			{
				SensorDataAccumulator.Process();
			}
		}

		public void AddPointsFromEvent(Phase phase, UIElement uiElement, PointerRoutedEventArgs args)
		{
			PointerDataProvider.AddPointsFromEvent(phase, uiElement, args);

			if (SensorDataAccumulator != null)
			{
				SensorDataAccumulator.Process();
			}
		}

		public Polygon CreateStrokePolygon(Spline spline)
		{
			return PolygonUtil.ConvertPolygon(SplineToPolygon(spline));
		}

		public void InitStrokePolygon(Spline spline, Polygon polygon)
		{
			PolygonUtil.ConvertPolygon(SplineToPolygon(spline), polygon);
		}

		public List<PointerData> GetPointerDataList()
		{
			if (SensorDataAccumulator == null)
				throw new Exception("InkBuilder is not constructed to collect pointer data.");

			var result = new List<PointerData>(SensorDataAccumulator.AccumulatedData);

			SensorDataAccumulator.Reset();

			return result;
		}
	}
}
