using System;
using System.Numerics;

namespace WacomInkDemoUWP
{
    public static class GeometryFactory
    {
        public static Vector2[] CreateSquare()
        {
            return new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, -0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-0.5f, 0.5f)
            };
        }

        public static Vector2[] CreateTriangle()
        {
            return new Vector2[]
            {
                new Vector2(-0.5f, -0.5f),
                new Vector2(0.5f, 0.0f),
                new Vector2(-0.5f, 0.5f)
            };
        }

        public static Vector2[] CreateEllipse(int pointsCount, float radiusX, float radiusY, double startAngleRadians = 0.0)
        {
            Vector2[] points = new Vector2[pointsCount];

            double radiansStep = Math.PI * 2 / pointsCount;

            for (var i = 0; i < pointsCount; i++)
            {
                double alpha = startAngleRadians + i * radiansStep;

                points[i] = new Vector2((float)(radiusX * Math.Cos(alpha)), (float)(radiusY * Math.Sin(alpha)));
            }

            return points;
        }

        public static Vector2[] CreateRect(float width, float height)
        {
            float halfW = width / 2.0f;
            float halfH = height / 2.0f;

            return new Vector2[]
            {
                new Vector2(-halfW, -halfH),
                new Vector2(halfW, -halfH),
                new Vector2(halfW, halfH),
                new Vector2(-halfW, halfH)
            };
        }

    }
}
