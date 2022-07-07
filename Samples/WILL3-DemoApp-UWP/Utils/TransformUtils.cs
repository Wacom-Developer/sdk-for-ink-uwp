using System.Numerics;
using Wacom.Ink.Geometry;

namespace WacomInkDemoUWP
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

    }
}
