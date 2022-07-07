using Wacom.Ink.Rendering;
using Windows.Foundation;

namespace WacomInkDemoUWP
{
    public static class Brushes
    {
        public static readonly AppBrush Circle = new AppBrush("wbr:Circle", VectorBrushGeometries.Circle);

        public static readonly AppBrush VerticalEllipse = new AppBrush("wbr:VEllipse", VectorBrushGeometries.VerticalEllipse);

        public static readonly AppRasterBrush Water = new AppRasterBrush(
            "wbr:Water",
            spacing: 0.1f,
            scattering: 0.25f,
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
                new PngDataFromAppResourceProvider("ms-appx:///Assets/shape_32x32.png")
            }
        );

        public static readonly AppRasterBrush Crayon = new AppRasterBrush(
            "wbr:Crayon",
            spacing: 0.3f,
            scattering: 0.0f,
            BlendMode.SourceOver,
            VectorBrushGeometries.Circle,
            new Size(512.0f, 512.0f),
            ParticleRotationMode.None,
            new PngDataFromAppResourceProvider("ms-appx:///Assets/fill.png"),
            new PngDataFromAppResourceProvider[]
            {
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_0_128x128.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_1_64x64.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_2_32x32.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_3_16x16.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_4_8x8.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_5_4x4.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_6_2x2.png"),
                new PngDataFromAppResourceProvider("ms-appx:///Assets/FromBP/brushpen_shape_7_1x1.png")
            }
        );

    }
}
