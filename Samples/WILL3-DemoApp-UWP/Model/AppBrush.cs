using System;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization;
using Windows.Foundation;

namespace WacomInkDemoUWP
{
    class AppBrush
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

    class AppRasterBrush : AppBrush
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
}
