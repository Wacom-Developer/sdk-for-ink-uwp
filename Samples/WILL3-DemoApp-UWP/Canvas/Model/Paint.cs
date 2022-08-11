using System;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization;
using Windows.Foundation;

namespace WacomInkDemoUWP
{
    public class AppBrush
    {
        public string BrushUri { get; }
        public float Spacing { get; }
        public BlendMode BlendMode { get; }
        public VectorBrush VectorBrush { get; }

        public AppBrush(string brushUri, VectorBrush vectorBrush, float spacing = 0.0f, BlendMode blendMode = BlendMode.SourceOver)
        {
            BrushUri = brushUri;
            VectorBrush = vectorBrush;
            BlendMode = blendMode;
            Spacing = spacing;
        }

        public override int GetHashCode()
        {
            return BrushUri.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (obj is AppBrush other) && (BrushUri == other.BrushUri);
        }
    }

    public class AppRasterBrush : AppBrush
    {
        public float Scattering { get; }
        public Size FillTileSize { get; }
        public ParticleRotationMode RotationMode { get; }
        public PngDataProvider m_fillTexturePngs;
        public PngDataProvider[] m_shapeTexturesPngs;

        // Cached rendering brush
        public ParticleBrush ParticleBrush { get; private set; }

        public AppRasterBrush(
            string brushUri,
            float spacing,
            float scattering,
            BlendMode blendMode,
            VectorBrush vectorBrush,
            Size fillTileSize,
            ParticleRotationMode rotationMode,
            PngDataProvider fillTexturePngs,
            PngDataProvider[] shapeTexturesPngs) : base(brushUri, vectorBrush, spacing, blendMode)
        {
            Scattering = scattering;
            FillTileSize = fillTileSize;
            RotationMode = rotationMode;

            m_fillTexturePngs = fillTexturePngs;
            m_shapeTexturesPngs = shapeTexturesPngs;

            ParticleBrush = null;
        }

        internal static AppRasterBrush CreateForComparison()
        {
            return new AppRasterBrush(
                brushUri: "",
                spacing: 1.0f,
                scattering: 0.0f,
                blendMode: BlendMode.SourceOver,
                vectorBrush: null,
                fillTileSize: new Size(0.0, 0.0),
                rotationMode: ParticleRotationMode.None,
                fillTexturePngs: null,
                shapeTexturesPngs: null);
        }

        public override int GetHashCode()
        {
            return BrushUri.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (obj is AppRasterBrush other) && (BrushUri == other.BrushUri);
        }

        public async Task LoadBrushTexturesAsync(Graphics graphics)
        {
            int mipMapLevelsCount = m_shapeTexturesPngs.Length;

            if (mipMapLevelsCount == 0)
                throw new Exception($"No shape textures for brush {BrushUri}");

            PixelData shapePixelData = await m_shapeTexturesPngs[0].GetPixelDataAsync();
            PixelData curPixelData = shapePixelData;

            for (int i = 1; i < mipMapLevelsCount; i++)
            {
                PixelData newPixelData = await m_shapeTexturesPngs[i].GetPixelDataAsync();
                curPixelData.Next = newPixelData;
                curPixelData = newPixelData;
            }

            PixelData fillPixelData = await m_fillTexturePngs.GetPixelDataAsync();

            ParticleBrush particleBrush = new ParticleBrush
            {
                ShapeTexture = graphics.CreateTexture(shapePixelData),
                FillTexture = graphics.CreateTexture(fillPixelData),
                FillTileSize = FillTileSize,
                RotationMode = RotationMode,
                Scattering = Scattering
            };

            ParticleBrush = particleBrush;
        }
    }

    public class Paint
    {
        public AppBrush Brush { get; }
        public string BrushUri { get; private set; }
        public float ScaleX { get; private set; } = 1.0f;
        public float ScaleY { get; private set; } = 1.0f;
        public float OffsetX { get; private set; } = 0.0f;
        public float OffsetY { get; private set; } = 0.0f;
        public BlendMode StrokeBlendMode { get; set; } = BlendMode.SourceOver;

        #region Constructors

        public Paint(AppBrush brush,
            float scaleX,
            float scaleY,
            float offsetX,
            float offsetY,
            BlendMode strokeBlendMode)
        {
            Brush = brush;

            BrushUri = brush.BrushUri;
            ScaleX = scaleX;
            ScaleY = scaleY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            StrokeBlendMode = strokeBlendMode;
        }

        public Paint(AppBrush brush, Wacom.Ink.Serialization.Model.Style style)
        {
            Brush = brush;

            Init(brush.BrushUri, style);
        }

        #endregion

        public void Init(string brushUri, Wacom.Ink.Serialization.Model.Style style)
        {
            BrushUri = brushUri;

            Wacom.Ink.Serialization.Model.PathPointProperties props = style.PathPointProperties;

            OffsetX = props.OffsetX;
            OffsetY = props.OffsetY;
            ScaleX = props.ScaleX;
            ScaleY = props.ScaleY;

            StrokeBlendMode = RenderModeUriToBlendMode(style.RenderModeUri);
        }

        public override int GetHashCode()
        {
            return BrushUri.GetHashCode() ^
                    ScaleX.GetHashCode() ^
                    ScaleY.GetHashCode() ^
                    OffsetX.GetHashCode() ^
                    OffsetY.GetHashCode() ^
                    StrokeBlendMode.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return (obj is Paint other) &&
                    (BrushUri == other.BrushUri) &&
                    (ScaleX == other.ScaleX) &&
                    (ScaleY == other.ScaleY) &&
                    (OffsetX == other.OffsetX) &&
                    (OffsetY == other.OffsetY) &&
                    (StrokeBlendMode == other.StrokeBlendMode);
        }

        public static BlendMode RenderModeUriToBlendMode(string renderModeUri)
        {
            switch (renderModeUri)
            {
                case BlendModeURIs.SourceOver:
                    return BlendMode.SourceOver;

                case BlendModeURIs.Max:
                    return BlendMode.Max;

                case BlendModeURIs.DestinationOver:
                    return BlendMode.DestinationOver;

                case BlendModeURIs.DestinationOut:
                    return BlendMode.DestinationOut;

                case BlendModeURIs.Lighter:
                    return BlendMode.Lighter;

                case BlendModeURIs.Copy:
                    return BlendMode.Copy;

                case BlendModeURIs.Min:
                    return BlendMode.Min;

                default:
                    return BlendMode.SourceOver;
            }
        }

    }
}
