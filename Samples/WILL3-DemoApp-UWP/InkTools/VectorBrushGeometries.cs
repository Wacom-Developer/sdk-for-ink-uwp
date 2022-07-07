using Wacom.Ink.Geometry;

namespace WacomInkDemoUWP
{
    public static class VectorBrushGeometries
    {
        public static readonly VectorBrush Circle = new VectorBrush(
            BrushPolygon.CreateNormalized(0.0f, GeometryFactory.CreateEllipse(4, 1.0f, 1.0f)),
            BrushPolygon.CreateNormalized(2.0f, GeometryFactory.CreateEllipse(8, 1.0f, 1.0f)),
            BrushPolygon.CreateNormalized(6.0f, GeometryFactory.CreateEllipse(16, 1.0f, 1.0f)),
            BrushPolygon.CreateNormalized(18.0f, GeometryFactory.CreateEllipse(32, 1.0f, 1.0f)));

        public static readonly VectorBrush VerticalEllipse = new VectorBrush(
            BrushPolygon.CreateNormalized(0.0f, GeometryFactory.CreateRect(0.2f, 1.0f)),
            BrushPolygon.CreateNormalized(2.0f, GeometryFactory.CreateEllipse(8, 0.3f, 1.0f)),
            BrushPolygon.CreateNormalized(6.0f, GeometryFactory.CreateEllipse(16, 0.3f, 1.0f)),
            BrushPolygon.CreateNormalized(18.0f, GeometryFactory.CreateEllipse(32, 0.3f, 1.0f)));

        public static readonly VectorBrush Triangle = new VectorBrush(
            BrushPolygon.CreateNormalized(0.0f, GeometryFactory.CreateTriangle()));

        public static readonly VectorBrush Square = new VectorBrush(
            BrushPolygon.CreateNormalized(0.0f, GeometryFactory.CreateSquare()));
    }
}
