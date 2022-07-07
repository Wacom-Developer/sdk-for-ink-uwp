using System;

namespace WacomInkDemoUWP
{
    public static class MathFunctions
    {
        public static float Sigmoid(float t, float k)
        {
            return (1 + k) * t / (Math.Abs(t) + k);
        }

        public static float Sigmoid1(float v, float p, float minValue = 0.0f, float maxValue = 1.0f)
        {
            float middle = (maxValue + minValue) * 0.5f;
            float halfInterval = (maxValue - minValue) * 0.5f;
            float t = (v - middle) / halfInterval;
            float s = Sigmoid(t, p);

            return middle + s * halfInterval;
        }

        public static float MapTo(float value, Range src, Range dest)
        {
            return dest.min + ((src.Clamp(value) - src.min) / (src.max - src.min)) * (dest.max - dest.min);
        }

        public static float MapTo(float value, Range src, Range dest, Func<float, float> remapFunction)
        {
            if (remapFunction != null)
            {
                value = remapFunction(src.Clamp(value));
                src.min = remapFunction(src.min);
                src.max = remapFunction(src.max);
            }

            return MapTo(value, src, dest);
        }
    }
}