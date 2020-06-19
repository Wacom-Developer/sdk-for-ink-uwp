using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;


using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;

namespace Wacom
{

    /// <summary>
    /// Abstract base class for raster based drawing tools 
    /// </summary>
    abstract class RasterDrawingTool : DrawingTool
    {
        /// <summary>
        /// ParticleBrush to render with
        /// </summary>
        public ParticleBrush Brush { get; } = new ParticleBrush();

        /// <summary>
        /// Particle Spacing 
        /// </summary>
        /// <remarks>In general, value of 1 means that the particles are next to each other; less than 1 – they overlap; greater than 1 - they are separated.</remarks>
        public float ParticleSpacing { get; protected set; }

        /// <summary>
        /// Pixel info for brush fill
        /// </summary>
        public PixelInfo Fill { get; protected set; }

        /// <summary>
        /// Pixel info for brush shape
        /// </summary>
        public PixelInfo Shape { get; protected set; }

        public ProcessorResult<List<float>> Path { get; private set; }

        public RasterInkBuilder InkBuilder { get; } = new RasterInkBuilder();

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Begin, uiElement, args);
            Path = InkBuilder.GetPath();
            PointsAdded?.Invoke(this, null);
        }

        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.Update, uiElement, args);
            Path = InkBuilder.GetPath();
            PointsAdded?.Invoke(this, null);
        }

        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            InkBuilder.AddPointsFromEvent(Phase.End, uiElement, args);
            Path = InkBuilder.GetPath();
            DrawingFinished?.Invoke(this, BlendCurrentStroke);
        }

    }


    /// <summary>
    /// Holds image pixel data 
    /// </summary>
    public class PixelInfo
    {
        /// <summary>
        /// Bitmap pixel data (for rendering)
        /// </summary>
        public PixelData PixelData { get; }

        /// <summary>
        /// Original image file data (for serialization)
        /// </summary>
        public byte[] ImageFileData { get; }

        public PixelInfo(Uri uri)
        {
            PixelData = Task.Run(async () => await GetPixelDataAsync(uri)).Result;
            ImageFileData = Task.Run(async () => await GetImageFileData(uri)).Result;
        }

        /// <summary>
        /// Loads bitmap pixel data from app resources
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static async Task<PixelData> GetPixelDataAsync(Uri uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);

            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                PixelDataProvider provider = await decoder.GetPixelDataAsync(
                  BitmapPixelFormat.Bgra8,
                  BitmapAlphaMode.Premultiplied,
                  new BitmapTransform(),
                  ExifOrientationMode.IgnoreExifOrientation,
                  ColorManagementMode.DoNotColorManage);

                var buffer = provider.DetachPixelData().AsBuffer();

                return new PixelData(buffer, decoder.PixelWidth, decoder.PixelHeight);
            }
        }

        /// <summary>
        /// Loads image file data from app resources
        /// </summary>
        private async static Task<byte[]> GetImageFileData(Uri uri)
        {
            //StreamResourceInfo sri = Application.GetResourceStream(uri);
            //if (sri != null)
            //{
            //    using (Stream s = sri.Stream)
            //    {
            //        byte[] data = new byte[s.Length];
            //        s.Read(data, 0, (int)s.Length);
            //        return data;
            //    }
            //}
            //return null;
            var fileToRead = await StorageFile.GetFileFromApplicationUriAsync(uri);

            
            using (BinaryReader fileReader = new BinaryReader(await fileToRead.OpenStreamForReadAsync()))
            {
                byte[] data = new byte[fileReader.BaseStream.Length];
                fileReader.Read(data, 0, data.Length);
                return data;
            }
            
        }

    }

    /// <summary>
    /// Raster drawing tool for rendering pencil-style output
    /// </summary>
    class PencilTool : RasterDrawingTool
    {
        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            minValue = 4,
            maxValue = 5,
            minSpeed = 80,
            maxSpeed = 1400,
        };
        private static readonly ToolConfig mAlphaConfig = new ToolConfig()
        {
            minValue = 0.05f,
            maxValue = 0.2f,
            minSpeed = 80,
            maxSpeed = 1400,
        };

        public PencilTool(Graphics graphics)
        {
            Fill = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_fill_11.png"));
            Shape = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_shape.png"));

            Brush.Scattering = 0.05f;
            Brush.RotationMode = ParticleRotationMode.RotateRandom;
            Brush.FillTileSize = new Size(32.0f, 32.0f);
            Brush.FillTexture = graphics.CreateTexture(Fill.PixelData);
            Brush.ShapeTexture = graphics.CreateTexture(Shape.PixelData);

            ParticleSpacing = 0.3f;
        }

        protected override ToolConfig SizeConfig => mSizeConfig; 
        protected override ToolConfig AlphaConfig => mAlphaConfig; 


    };

    /// <summary>
    /// Raster drawing tool for rendering water brush-style output
    /// </summary>
    class WaterBrushTool : RasterDrawingTool
    {
        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            minValue = 28,
            maxValue = 32,
            minSpeed = 38,
            maxSpeed = 1500,
            remap = v => (float)Math.Pow(v, 3)
        };
        private static readonly ToolConfig mAlphaConfig = new ToolConfig()
        {
            minValue = 0.02f, 
            maxValue = 0.25f, 
            minSpeed = 38,
            maxSpeed = 1500,
        };

        public WaterBrushTool(Graphics graphics)
        {
            Fill = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_fill_14.png"));
            Shape = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_shape.png"));

            ParticleSpacing = 0.15f;  
            Brush.Scattering = 0.05f; 
            Brush.RotationMode = ParticleRotationMode.RotateRandom;
            Brush.FillTileSize = new Size(32.0f, 32.0f);
            Brush.FillTexture = graphics.CreateTexture(Fill.PixelData);
            Brush.ShapeTexture = graphics.CreateTexture(Shape.PixelData);
        }
                                                                                 
        protected override ToolConfig SizeConfig => mSizeConfig; 
        protected override ToolConfig AlphaConfig => mAlphaConfig;
    }

    /// <summary>
    /// Raster drawing tool for rendering crayon-style output
    /// </summary>
    class CrayonTool : RasterDrawingTool
    {
        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            minValue = 18,
            maxValue = 28,
            minSpeed = 10,
            maxSpeed = 1400,
        };
        private static readonly ToolConfig mAlphaConfig = new ToolConfig()
        {
            minValue = 0.1f,
            maxValue = 0.6f,
            minSpeed = 10,
            maxSpeed = 1400,
        };

        public CrayonTool(Graphics graphics)
        {
            Fill = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_fill_17.png"));
            Shape = new PixelInfo(new Uri("ms-appx:///Assets/textures/essential_shape.png"));

            ParticleSpacing = 0.15f;
            Brush.Scattering = 0.05f;
            Brush.RotationMode = ParticleRotationMode.RotateRandom;
            Brush.FillTileSize = new Size(32.0f, 32.0f);
            Brush.FillTexture = graphics.CreateTexture(Fill.PixelData);
            Brush.ShapeTexture = graphics.CreateTexture(Shape.PixelData);
        }


        protected override ToolConfig SizeConfig => mSizeConfig; 
        protected override ToolConfig AlphaConfig => mAlphaConfig; 
    }

}
