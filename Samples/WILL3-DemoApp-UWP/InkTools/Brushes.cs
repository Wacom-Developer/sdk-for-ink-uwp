using Wacom.Ink.Rendering;
using Windows.Foundation;

namespace WacomInkDemoUWP
{
    static class Brushes
    {
        public static readonly AppBrush Circle = new AppBrush("wbr:Circle", VectorBrushGeometries.Circle);

        public static readonly AppBrush Ellipse = new AppBrush("wbr:Ellipse", VectorBrushGeometries.Ellipse);

        public static readonly AppRasterBrush Water = new AppRasterBrush(
            "wbr:Water",
            spacing: 0.1f,
            scattering: 0.2f,
            BlendMode.SourceOver,
            VectorBrushGeometries.Circle,
            new Size(32.0f, 32.0f),
            ParticleRotationMode.RotateRandom,
            new PngDataFromAppResourceProvider("ms-appx:///Assets/fill.png"),
            new PngDataFromAppResourceProvider[]
            {
                new PngDataFromAppResourceProvider("ms-appx:///Assets/shape_32x32.png")
            }
        );

        public static readonly AppRasterBrush Pencil = new AppRasterBrush(
            "wbr:Pencil",
            spacing: 0.3f,
            scattering: 0.05f,
            BlendMode.SourceOver,
            VectorBrushGeometries.Circle,
            new Size(32.0f, 32.0f),
            ParticleRotationMode.RotateRandom,
            new PngDataFromAppResourceProvider("ms-appx:///Assets/fill.png"),
            new PngDataFromAppResourceProvider[]
            {
                new PngDataFromAppResourceProvider("ms-appx:///Assets/pencil_shape.png")
            }
        );

        public static readonly AppRasterBrush Crayon = new AppRasterBrush(
            "wbr:Crayon",
			spacing: 0.14f,
			scattering: 0.2f,
			BlendMode.SourceOver,
            VectorBrushGeometries.Circle,
            new Size(128.0f, 128.0f),
            ParticleRotationMode.RotateRandom,
            new PngDataFromAppResourceProvider("ms-appx:///Assets/fill.png"),
            new PngDataFromAppResourceProvider[]
            {
                new PngDataFromAppResourceProvider("ms-appx:///Assets/crayon_shape.png"),
            }
        );
    }
}
