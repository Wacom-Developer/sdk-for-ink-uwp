using System;

namespace Wacom
{
    /// <summary>
    /// Types of brush supported
    /// </summary>
    public enum BrushType
    {
        Vector,
        Raster
    };

    /// <summary>
    /// Styles of sample vector brushes
    /// </summary>
    public enum VectorBrushStyle
    {
        Selection,
        Pen,
        Felt,
        Brush
    };

    /// <summary>
    /// Styles of sample raster brushes
    /// </summary>
    public enum RasterBrushStyle
    {
        Selection,
        Pencil,
        WaterBrush,
        Crayon
    };

}
