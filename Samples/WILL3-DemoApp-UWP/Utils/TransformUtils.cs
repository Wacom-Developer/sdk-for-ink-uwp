using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;

namespace Wacom
{
	public class TransformUtils
	{
		public static Spline TransformSplineXY(Spline spline, Matrix3x2 matrix3x2)
		{
            var transformedSpline = spline.Clone();

			LayoutMask layout = spline.LayoutMask;
			int stride = layout.GetChannelsCount();
			int xIndex = layout.GetChannelIndex(PathPoint.Property.X);
			int yIndex = layout.GetChannelIndex(PathPoint.Property.Y);

			for (int i = 0; i < transformedSpline.Path.Count; i += stride)
			{
				int xCurIndex = xIndex + i;
				int yCurIndex = yIndex + i;

				Vector2 position = new Vector2(transformedSpline.Path[xCurIndex], transformedSpline.Path[yCurIndex]);

				Vector2 transformed = Vector2.Transform(position, matrix3x2);

				transformedSpline.Path[xCurIndex] = transformed.X;
				transformedSpline.Path[yCurIndex] = transformed.Y;
			}
            return transformedSpline;
		}

		public static void TransformPolysXY(List<List<Vector2>> polygons, Matrix3x2 matrix3x2)
		{
			if (!matrix3x2.IsIdentity)
			{
				foreach (var polygon in polygons)
				{
					TransformPolyXY(polygon, matrix3x2);
				}
			}
		}

		public static void TransformPolyXY(List<Vector2> polygon, Matrix3x2 matrix3x2)
		{
			if (!matrix3x2.IsIdentity)
			{
				for (int i = 0; i < polygon.Count; i++)
				{
					Vector2 point = polygon[i];

					Vector2 transformed = Vector2.Transform(point, matrix3x2);

					polygon[i] = transformed;
				}
			}
		}
	}
}
