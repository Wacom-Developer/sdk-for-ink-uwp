using System.Collections.Generic;
using System.Numerics;
using Wacom.Ink.Rendering;

namespace WacomInkDemoUWP
{
    using DIPolygon = List<Vector2>;

    static class ListOfPolygonsExtensions
    {
        public static Polygon ToPolygon(this List<DIPolygon> src)
        {
            Polygon dest = new Polygon();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }

            return dest;
        }

        public static void ToPolygon(this List<DIPolygon> src, Polygon dest)
        {
            dest.RemoveAllContours();

            foreach (var polygon in src)
            {
                dest.AddContour(polygon);
            }
        }

    }
}
