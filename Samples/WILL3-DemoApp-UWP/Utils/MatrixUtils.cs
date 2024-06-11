using System;
using System.Numerics;

namespace WacomInkDemoUWP
{
    static class Matrix3x2Ext
    {
        public static float GetScale(this Matrix3x2 transform)
        {
            return MathF.Sqrt(MathF.Abs(transform.GetDeterminant()));
        }
    }
}
