using System;
using System.Collections.Generic;
using Wacom.Ink.Geometry;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace Wacom
{
	public class RasterInkBuilder : StockRasterInkBuilder
	{
		SensorDataAccumulator SensorDataAccumulator { get; set; }

		public RasterInkBuilder(bool storeSensorData)
		{
			if (storeSensorData)
			{
				SensorDataAccumulator = new SensorDataAccumulator();
				SensorDataAccumulator.SetDataProvider(PointerDataProvider);
			}
		}

		public void UpdatePipeline(LayoutMask layoutMask, Calculator calculator, float spacing)
		{
			PathProducer.LayoutMask = layoutMask;
			PathProducer.PathPointCalculator = calculator;
			SplineInterpolator.Spacing = spacing;
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