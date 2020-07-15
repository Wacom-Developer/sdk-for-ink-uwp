using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected abstract float PreviousAlpha { get; set; }

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

        public override Calculator GetCalculator(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return CalculatorForMouseAndTouch;
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return CalculatorForStylus;
                default:
                    throw new Exception("Unknown input device type");
            }
        }


        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected abstract PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next);


        /// <summary>
        /// Calculator delegate for input from mouse input
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected PathPoint CalculatorForMouseAndTouch(PointerData previous, PointerData current, PointerData next)
        {
            var size = current.ComputeValueBasedOnSpeed(previous, next, SizeConfig.minValue, SizeConfig.maxValue, SizeConfig.initValue, SizeConfig.finalValue, SizeConfig.minSpeed, SizeConfig.maxSpeed, SizeConfig.remap);

            if (size.HasValue)
            {
                PreviousSize = size.Value;
            }
            else
            {
                size = PreviousSize;
            }

            var alpha = current.ComputeValueBasedOnSpeed(previous, next, AlphaConfig.minValue, AlphaConfig.maxValue, AlphaConfig.initValue, AlphaConfig.finalValue, AlphaConfig.minSpeed, AlphaConfig.maxSpeed, AlphaConfig.remap);

            if (alpha.HasValue)
            {
                PreviousAlpha = alpha.Value;
            }
            else
            {
                alpha = PreviousAlpha;
            }
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Alpha = alpha
            };

            return pp;
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
            PixelData = Task.Run(async () => await Utils.GetPixelDataAsync(uri)).Result;
            ImageFileData = Task.Run(async () => await Utils.GetImageFileData(uri)).Result;
        }


    }

    /// <summary>
    /// Raster drawing tool for rendering pencil-style output
    /// </summary>
    class PencilTool : RasterDrawingTool
    {
        private const float MinSize = 4;
        private const float MaxSize = 10;
        private const float MinAlpha = 0.1f;
        private const float MaxAlpha = 0.7f;
        private const float MaxSpeed = 15000;
        private const float MinAltitudeAngle = 0.4f;

        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            minValue = MinSize,
            maxValue = MaxSize,
            minSpeed = 80,
            maxSpeed = MaxSpeed,
            remap = v => 1 - v
        };
        private static readonly ToolConfig mAlphaConfig = new ToolConfig()
        {
            minValue = MinAlpha,
            maxValue = MaxAlpha,
            minSpeed = 80,
            maxSpeed = MaxSpeed,
            remap = v => 1 - v
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

        protected override float PreviousSize { get; set; } = 6;
        protected override float PreviousAlpha { get; set; } = 0.2f;

        public override PathPointLayout GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return new PathPointLayout(PathPoint.Property.X,
                                                PathPoint.Property.Y,
                                                PathPoint.Property.Size,
                                                PathPoint.Property.Alpha);
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return new PathPointLayout(PathPoint.Property.X,
                                                PathPoint.Property.Y,
                                                PathPoint.Property.Size,
                                                PathPoint.Property.Alpha,
                                                PathPoint.Property.Rotation,
                                                PathPoint.Property.OffsetX,
                                                PathPoint.Property.OffsetY);
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected override PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            var cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            var sinAzimuthAngle  = (float)Math.Sin(current.AzimuthAngle.Value);
            var cosAzimuthAngle  = (float)Math.Cos(current.AzimuthAngle.Value);
            // calculate the offset of the pencil tip due to tilted position
            var x = sinAzimuthAngle * cosAltitudeAngle;
            var y = cosAltitudeAngle * cosAzimuthAngle;
            var offsetY = 5 * -x;
            var offsetX = 5 * -y;
            // compute the rotation
            var rotation = current.ComputeNearestAzimuthAngle(previous);
            // Normalize the tilt be minimum seen altitude angle and the maximum with the pen straight up
            const float piBy2 = (float)(Math.PI / 2);
            var tiltScale = Math.Min(1f, ((piBy2 - current.AltitudeAngle.Value) / (piBy2 - MinAltitudeAngle)));

            // now, based on the tilt of the pencil the size of the brush size is increasing, as the
            // pencil tip is covering a larger area
            var size = Math.Max(MinSize, MinSize + (MaxSize - MinSize) * tiltScale);

            // Change the intensity of alpha value by pressure of speed, if available else use speed
            var alpha = (!current.Force.HasValue)
                ? current.ComputeValueBasedOnSpeed(previous, next, MinAlpha, MaxAlpha, null, null, 0f, MaxSpeed)
                : ComputeValueBasedOnPressure(current, MinAlpha, MaxAlpha, 0.0f, 1.0f);

            if (!alpha.HasValue)
            {
                alpha = PreviousAlpha;
            }
            else
            {
                PreviousAlpha = alpha.Value;
            }
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Alpha = alpha,
                Rotation = rotation,
                OffsetX = offsetX,
                OffsetY = offsetY
            };
            return pp;
        }


    };

    /// <summary>
    /// Raster drawing tool for rendering water brush-style output
    /// </summary>
    class WaterBrushTool : RasterDrawingTool
    {
        private const float MinSize = 40;
        private const float MaxSize = 60;
        private const float MinAlpha = 0.2f;
        private const float MaxAlpha = 0.5f;
        private const float MaxSpeed = 7500;

        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            initValue = 40f,
            finalValue = 40f,
            minValue = 40,
            maxValue = 60,
            minSpeed = 38,
            maxSpeed = 1500,
            remap = v => (float)Math.Pow(v, 1.17f)
        };
        private static readonly ToolConfig mAlphaConfig = new ToolConfig()
        {
            initValue = 0.05f,
            finalValue = 0.05f,
            minValue = 0.2f, 
            maxValue = 0.5f, 
            minSpeed = 1000,
            maxSpeed = 3500,
            remap = v => (float)Math.Pow(v, 1.17f)
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

        protected override float PreviousSize { get; set; } = 28;
        protected override float PreviousAlpha { get; set; } = 0.02f;

        public override PathPointLayout GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return new PathPointLayout(PathPoint.Property.X,
                                                    PathPoint.Property.Y,
                                                    PathPoint.Property.Size,
                                                    PathPoint.Property.Alpha);
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return new PathPointLayout(PathPoint.Property.X,
                                                    PathPoint.Property.Y,
                                                    PathPoint.Property.Size,
                                                    PathPoint.Property.Rotation,
                                                    PathPoint.Property.OffsetX,
                                                    PathPoint.Property.OffsetY,
                                                    PathPoint.Property.Alpha);
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected override PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            var size = (!current.Force.HasValue)
                ? current.ComputeValueBasedOnSpeed(previous, next, 30f, 80f, null, null, 0f, 3500f, v => (float)Math.Pow(v, 1.17f))
                : ComputeValueBasedOnPressure(current, 30f, 80f, 0.0f, 1.0f, false, v => (float)Math.Pow(v, 1.17));

            if (!size.HasValue)
            {
                size = PreviousSize;
            }
            else
            {
                PreviousSize = size.Value;
            }

            // Change the intensity of alpha value by pressure or speed
            var alpha = (!current.Force.HasValue)
                ? current.ComputeValueBasedOnSpeed(previous, next, MinAlpha, MaxAlpha, null, null, 0f, 3500f, v => (float)Math.Pow(v, 1.17))
                : ComputeValueBasedOnPressure(current, MinAlpha, MaxAlpha, 0.0f, 1.0f, false, v => (float)Math.Pow(v, 1.17));
            if (!alpha.HasValue)
            {
                alpha = PreviousAlpha;
            }
            else
            {
                PreviousAlpha = alpha.Value;
            }
            var cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            var sinAzimuthAngle  = (float)Math.Sin(current.AzimuthAngle.Value);
            var cosAzimuthAngle  = (float)Math.Cos(current.AzimuthAngle.Value);
            // calculate the offset of the pencil tip due to tilted position
            var x = sinAzimuthAngle * cosAltitudeAngle;
            var y = cosAltitudeAngle * cosAzimuthAngle;
            var offsetY = 5 * -x;
            var offsetX = 5 * -y;

            var rotation = current.ComputeNearestAzimuthAngle(previous);
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Alpha = alpha,
                Rotation = rotation,
                OffsetX = offsetX,
                OffsetY = offsetY
            };
            return pp;
        }

    }

    /// <summary>
    /// Raster drawing tool for rendering crayon-style output
    /// </summary>
    class CrayonTool : RasterDrawingTool
    {
        private const float MinSize = 25;
        private const float MaxSize = 50;
        private const float MinAlpha = 0.1f;
        private const float MaxAlpha = 0.7f;
        private const float MaxSpeed = 15000;
        private const float MinAltitudeAngle = 0.4f;

        private static readonly ToolConfig mSizeConfig = new ToolConfig()
        {
            minValue = MinSize,
            maxValue = MaxSize,
            minSpeed = 10,
            maxSpeed = MaxSpeed,
            remap = v => 1 - v
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

        protected override float PreviousSize { get; set; } = MinSize;
        protected override float PreviousAlpha { get; set; } = 0.1f;

        public override PathPointLayout GetLayout(Windows.Devices.Input.PointerDeviceType deviceType)
        {
            switch (deviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    return new PathPointLayout(PathPoint.Property.X,
                                                    PathPoint.Property.Y,
                                                    PathPoint.Property.Size,
                                                    PathPoint.Property.Alpha);
                case Windows.Devices.Input.PointerDeviceType.Pen:
                    return new PathPointLayout(PathPoint.Property.X,
                                                    PathPoint.Property.Y,
                                                    PathPoint.Property.Size,
                                                    PathPoint.Property.Alpha,
                                                    PathPoint.Property.Rotation,
                                                    PathPoint.Property.OffsetX,
                                                    PathPoint.Property.OffsetY);
                default:
                    throw new Exception("Unknown input device type");
            }
        }

        /// <summary>
        /// Calculator delegate for input from a stylus (pen)
        /// Calculates the path point properties based on pointer input.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        /// <param name="next"></param>
        /// <returns>PathPoint with calculated properties</returns>
        protected override PathPoint CalculatorForStylus(PointerData previous, PointerData current, PointerData next)
        {
            // calculate the offset of the pencil tip due to tilted position
            var cosAltitudeAngle = (float)Math.Cos(current.AltitudeAngle.Value);
            var sinAzimuthAngle  = (float)Math.Sin(current.AzimuthAngle.Value);
            var cosAzimuthAngle  = (float)Math.Cos(current.AzimuthAngle.Value);
            var x                = sinAzimuthAngle * cosAltitudeAngle;
            var y                = cosAltitudeAngle * cosAzimuthAngle;
            var offsetY          = 5f * -x;
            var offsetX          = 5f * -y;
            // compute the rotation
            var rotation = current.ComputeNearestAzimuthAngle(previous);
            // Normalize the tilt be minimum seen altitude angle and the maximum with the pen straight up
            const float piBy2 = (float)(Math.PI / 2);
            var tiltScale = Math.Min(1f, (piBy2 - current.AltitudeAngle.Value) / (piBy2 - MinAltitudeAngle));


            var size = Math.Max(MinSize, MinSize + (MaxSize - MinSize) * tiltScale);

            //var rotation = current.ComputeNearestAzimuthAngle(previous);
            // Change the intensity of alpha value by pressure of speed
            var alpha = (!current.Force.HasValue)
                ? current.ComputeValueBasedOnSpeed(previous, next, MinAlpha, MaxAlpha, null, null, 0f, 3500f, v => 1 - v)
                : ComputeValueBasedOnPressure(current, 0.1f, 0.7f, 0.0f, 1.0f);
            if (!alpha.HasValue)
            {
                alpha = PreviousAlpha;
            }
            else
            {
                PreviousAlpha = alpha.Value;
            }
            PathPoint pp = new PathPoint(current.X, current.Y)
            {
                Size = size,
                Alpha = alpha,
                Rotation = rotation,
                OffsetX = offsetX,
                OffsetY = offsetY
            };
            return pp;
        }
    }

}
