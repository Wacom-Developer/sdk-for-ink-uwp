using System.Collections.Generic;
using System.Numerics;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    using DIPolygon = List<Vector2>;

    public static class PolygonUtil
    {
        public static Polygon ConvertPolygon(List<DIPolygon> src)
        {
            Polygon dest = new Polygon();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }

            return dest;
        }

        public static void ConvertPolygon(List<DIPolygon> src, Polygon dest)
        {
            dest.RemoveAllContours();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }
        }

    }
}
