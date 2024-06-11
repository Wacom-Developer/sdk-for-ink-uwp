using System.Collections.Generic;
using System.Diagnostics;
using Wacom.Ink.Geometry;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;


namespace WacomInkDemoUWP
{
    static class PointerDataProviderExtensions
    {
        public static void AddPointsFromEvent(this PointerDataProvider accumulator, Phase phase, PointerEventArgs args, bool useIntermediatePoints = true)
        {
            if (useIntermediatePoints)
            {
                AddPoints(phase, accumulator, args.GetIntermediatePoints());
            }
            else
            {
                accumulator.Add(ConvertPoint(phase, args.CurrentPoint));
            }
        }

        public static void AddPointsFromEvent(this PointerDataProvider accumulator, Phase phase, UIElement uiElement, PointerRoutedEventArgs args, bool useIntermediatePoints = true)
        {
            if (useIntermediatePoints)
            {
                AddPoints(phase, accumulator, args.GetIntermediatePoints(uiElement));
            }
            else
            {
                accumulator.Add(ConvertPoint(phase, args.GetCurrentPoint(uiElement)));
            }
        }

        private static void AddPoints(Phase phase, PointerDataProvider accumulator, IList<PointerPoint> points)
        {
            switch (phase)
            {
                case Phase.Begin:
                    Debug.Assert(points.Count == 1);
                    accumulator.Add(ConvertPoint(phase, points[0]));
                    break;

                case Phase.End:
                    for (int i = points.Count - 1; i > 0; i--)
                    {
                        accumulator.Add(ConvertPoint(Phase.Update, points[i]));
                    }

                    accumulator.Add(ConvertPoint(Phase.End, points[0]));
                    break;

                default:
                    for (int i = points.Count - 1; i >= 0; i--)
                    {
                        accumulator.Add(ConvertPoint(phase, points[i]));
                    }
                    break;
            }
        }

        private static PointerData ConvertPoint(Phase phase, PointerPoint pp)
        {
            PointerData pointerData = new PointerData((float)pp.Position.X, (float)pp.Position.Y, phase, (long)pp.Timestamp);

            PointerPointProperties props = pp.Properties;
            pointerData.Force = props.Pressure;

            PointerData.CalculateAltitudeAndAzimuth(props.XTilt, props.YTilt, out float altitude, out float azimuth);
            pointerData.AltitudeAngle = altitude;
            pointerData.AzimuthAngle = azimuth;

            return pointerData;
        }
    }
}
