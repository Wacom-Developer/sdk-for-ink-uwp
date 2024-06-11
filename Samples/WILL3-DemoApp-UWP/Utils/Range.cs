using System;

namespace WacomInkDemoUWP
{
    struct Range
    {
        public float min;
        public float max;

        public Range(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Clamp(float value)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
