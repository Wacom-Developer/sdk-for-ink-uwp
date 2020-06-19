using Wacom.Ink.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Wacom.Ink.Rendering;


namespace Wacom
{
	using DIPolygon = List<Vector2>;
	public static class PolygonUtil
	{
		static public Polygon ConvertPolygon(List<DIPolygon> src)
		{
			Polygon dest = new Polygon();

			foreach (var polygon in src)
			{
				dest.AddContour(polygon);
			}

			return dest;
		}

		static public void ConvertPolygon(List<DIPolygon> src, Polygon dest)
		{
			dest.RemoveAllContours();

			foreach (var polygon in src)
			{
				dest.AddContour(polygon);
			}
		}

	}
}
