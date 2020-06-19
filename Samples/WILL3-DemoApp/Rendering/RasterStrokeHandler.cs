﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    public class RasterStrokeHandler : StrokeHandler
    {
        #region Fields

        /// <summary>
        /// InkBuilder handling pipeline stages for building raster ink 
        /// </summary>
        private RasterInkBuilder RasterInkBuilder => ActiveTool.InkBuilder;

        /// <summary>
        /// List of completed strokes
        /// </summary>
        private List<RasterInkStroke> m_dryStrokes = new List<RasterInkStroke>();

        private Graphics m_graphics;
        private uint m_startRandomSeed;
        private Random mRand = new Random();
        private DrawStrokeResult m_drawStrokeResult;
        private ParticleList m_addedInterpolatedSpline;
        private ParticleList m_predictedInterpolatedSpline;
        private RasterBrushStyle mBrushStyle;
        private StrokeConstants mStrokeConstants = new StrokeConstants();

        private RasterDrawingTool ActiveTool = null;


        #endregion

        #region Properties

        public override BrushType BrushType { get { return BrushType.Raster; } }

        public override MediaColor BrushColor
        {
            get { return mStrokeConstants.Color; }
            set { mStrokeConstants.Color = value; }
        }

        public override InkBuilder InkBuilder => RasterInkBuilder;

        #endregion

        #region Constructor

        public RasterStrokeHandler(Renderer renderer, RasterBrushStyle brushStyle, MediaColor color, Graphics graphics)
          : base(renderer, color)
        {
            m_graphics = graphics;
            mBrushStyle = brushStyle;
        }

        #endregion

        #region IDispose support

        public override void Dispose()
        {
            m_dryStrokes.Clear();
            Utils.SafeDispose(mStrokeConstants);
        }

        #endregion


        #region Public Interface

        public override void SetBrushStyle(RasterBrushStyle brushStyle)
        {
            CreateBrush(brushStyle);
        }

        public override void OnPressed(UIElement uiElement, PointerRoutedEventArgs args)
        {
            m_startRandomSeed = (uint)mRand.Next();
            base.OnPressed(uiElement, args);
            ActiveTool.OnPressed(uiElement, args);
        }
        public override void OnMoved(UIElement uiElement, PointerRoutedEventArgs args)
        {
            ActiveTool.OnMoved(uiElement, args);
        }
        public override void OnReleased(UIElement uiElement, PointerRoutedEventArgs args)
        {
            ActiveTool.OnReleased(uiElement, args);
        }

        public override void SetupStrokeTool(Windows.Devices.Input.PointerDevice device)
        {
            (PathPointLayout, Calculator) layoutAndCalc;

            switch (device.PointerDeviceType)
            {
                case Windows.Devices.Input.PointerDeviceType.Mouse:
                case Windows.Devices.Input.PointerDeviceType.Touch:
                    layoutAndCalc = ActiveTool.GetLayoutAndCalulatorForMouse();
                    break;

                case Windows.Devices.Input.PointerDeviceType.Pen:
                    layoutAndCalc = ActiveTool.GetLayoutAndCalulatorForStylus();
                    break;

                default:
                    throw new Exception("Unknown input device type");
            }
            RasterInkBuilder.UpdatePipeline(layoutAndCalc.Item1, layoutAndCalc.Item2, ActiveTool.ParticleSpacing);
        }

        public override bool IsSelecting => false;

        public override IEnumerable<Identifier> SelectedStrokes => null;

        public override void StartSelectionMode()
        {
            throw new NotImplementedException("Raster ink manipulation is not supported");
        }

        public override void StopSelectionMode()
        {
        }

        /// <summary>
        /// Draw stroke
        /// </summary>
        /// <param name="renderingContext">RenderingContext to draw to</param>
        /// <param name="o">Cached stroke (as object)</param>
        public override void DoRenderStroke(RenderingContext renderingContext, object o, bool translationLayerPainted)
        {
            RasterInkStroke stroke = (RasterInkStroke)o;
            renderingContext.DrawParticleStroke(stroke.Path, stroke.StrokeConstants, ActiveTool.Brush, Ink.Rendering.BlendMode.SourceOver, stroke.RandomSeed);
        }

        /// <summary>
        /// Clear all saved strokes
        /// </summary>
        public override void ClearStrokes()
        {
            m_dryStrokes.Clear();
            mSerializer = new Serializer();
        }

        /// <summary>
        /// Make the current stroke permanent
        /// </summary>
        /// <remarks>Copies the output of the render pipeline from InkBuilder to dry strokes</remarks>
        public override void StoreCurrentStroke(PointerDeviceType deviceType)
        {
            var allData = RasterInkBuilder.SplineInterpolator.AllData;
            var points = new List<float>();

            if (allData != null)
            {
                for (int i = 0; i < allData.Count; i++)
                {
                    points.Add(allData[i]);
                }

                if (points.Count > 0)
                {

                    var dryStroke = new RasterInkStroke(RasterInkBuilder,
                        deviceType,
                        points,
                        m_startRandomSeed,
                        CreateSerializationBrush($"will://examples/brushes/{Guid.NewGuid().ToString()}"),
                        mStrokeConstants.Clone(),
                        mSerializer.AddSensorData(deviceType, InkBuilder.GetPointerDataList()));
                    m_dryStrokes.Add(dryStroke);
                }
            }
        }

        public override InkModel Serialize()
        {
            mSerializer.Init();
            foreach (var stroke in m_dryStrokes)
            {
                mSerializer.EncodeStroke(stroke);
            }
            return mSerializer.InkDocument;
        }

        public override void RenderAllStrokes(RenderingContext context, IEnumerable<Identifier> excluded, Rect? clipRect)
        {
            foreach (var stroke in m_dryStrokes)
            {
                // Draw current stroke
                context.SetTarget(mRenderer.CurrentStrokeLayer);
                context.ClearColor(Colors.Transparent);

                DoRenderStroke(context, stroke, mRenderer.TranslationLayerPainted);

                // Blend stroke to Scene Layer
                context.SetTarget(mRenderer.SceneLayer);
                context.DrawLayer(mRenderer.CurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);

                // Blend Current Stroke to All Strokes Layer
                context.SetTarget(mRenderer.AllStrokesLayer);
                context.DrawLayer(mRenderer.CurrentStrokeLayer, null, Ink.Rendering.BlendMode.SourceOver);
            }
        }

        /// <summary>
        /// Handles brush-specific parts of drawing a new stroke segment
        /// </summary>
        /// <param name="updateRect">returns bounding rectangle of area requiring update</param>
        public override void DoRenderNewStrokeSegment(out Rect updateRect)
        {
            var result = ActiveTool.Path;

            m_addedInterpolatedSpline.Assign(result.Addition);
            m_predictedInterpolatedSpline.Assign(result.Prediction);

            // Draw the added stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.CurrentStrokeLayer);
            m_drawStrokeResult = mRenderer.RenderingContext.DrawParticleStroke(m_addedInterpolatedSpline, mStrokeConstants, ActiveTool.Brush, Ink.Rendering.BlendMode.SourceOver, m_drawStrokeResult.RandomGeneratorSeed);

            // Measure the predicted stroke
            Rect predictedStrokeRect = mRenderer.RenderingContext.MeasureParticleStrokeBounds(m_predictedInterpolatedSpline, mStrokeConstants, ActiveTool.Brush.Scattering);

            // Calculate the update rect for this frame
            updateRect = mRenderer.DirtyRectManager.GetUpdateRect(m_drawStrokeResult.DirtyRect, predictedStrokeRect);

            // Draw the predicted stroke
            mRenderer.RenderingContext.SetTarget(mRenderer.PrelimPathLayer);
            mRenderer.RenderingContext.DrawLayerAtPoint(mRenderer.CurrentStrokeLayer, updateRect, new Point(updateRect.X, updateRect.Y), Ink.Rendering.BlendMode.Copy);
            mRenderer.RenderingContext.DrawParticleStroke(m_predictedInterpolatedSpline, mStrokeConstants, ActiveTool.Brush, Ink.Rendering.BlendMode.SourceOver, m_drawStrokeResult.RandomGeneratorSeed);
        }

        /// <summary>
        /// Brush-specific handling of GraphicsReady event
        /// </summary>
        public override void DoGraphicsReady()
        {
            CreateBrush(mBrushStyle);
            RasterInkBuilder.LayoutUpdated += InkBuilder_LayoutUpdated;
        }

        private void InkBuilder_LayoutUpdated(object sender, EventArgs e)
        {
            uint channelMask = (uint)RasterInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;

            m_addedInterpolatedSpline = new ParticleList(channelMask);
            m_predictedInterpolatedSpline = new ParticleList(channelMask);
        }

        /// <summary>
        /// Loads serialized ink
        /// </summary>
        /// <param name="inkDocument"></param>
        public override void LoadInk(InkModel inkDocument)
        {
            base.LoadInk(inkDocument);
            m_dryStrokes = new List<RasterInkStroke>(RecreateDryStrokes(inkDocument));
        }

        #endregion

        #region Implementation

        private void CreateBrush(RasterBrushStyle brushStyle)
        {
            SetBrushStyle(brushStyle, m_graphics);
            mBrushStyle = brushStyle;
        }

        public void SetBrushStyle(RasterBrushStyle brushStyle, Graphics graphics)
        {
            StopSelectionMode();
            switch (mBrushStyle = brushStyle)
            {
                case RasterBrushStyle.Pencil:
                    ActiveTool = new PencilTool(graphics);
                    break;
                case RasterBrushStyle.WaterBrush:
                    ActiveTool = new WaterBrushTool(graphics);
                    break;
                case RasterBrushStyle.Crayon:
                    ActiveTool = new CrayonTool(graphics);
                    break;
                default:
                    throw new Exception("Unknown brush type");
            }
            ActiveTool.PointsAdded += OnPointsAdded;
        }

        public void OnPointsAdded(object sender, EventArgs args)
        {
            mRenderer.RenderNewStrokeSegment();
            mRenderer.RenderBackbuffer();
            mRenderer.PresentGraphics();
        }

        #endregion

        #region Serialization Support

        public Wacom.Ink.Serialization.Model.RasterBrush CreateSerializationBrush(string name)
        {
            return new Wacom.Ink.Serialization.Model.RasterBrush(name,
                                            (float)ActiveTool.Brush.FillTileSize.Width, 
                                            (float)ActiveTool.Brush.FillTileSize.Height,
                                            true,
                                            (RotationMode)ActiveTool.Brush.RotationMode,
                                            ActiveTool.Brush.Scattering,
                                            ((DistanceBasedInterpolator)ActiveTool.InkBuilder.SplineInterpolator).Spacing,
                                            ActiveTool.Fill.ImageFileData,
                                            new List<byte[]>() { ActiveTool.Shape.ImageFileData },
                                            new List<string>(),
                                            string.Empty,
                                            Wacom.Ink.Serialization.Model.BlendMode.SourceOver
                                           );
        }

        private List<RasterInkStroke> RecreateDryStrokes(InkModel inkDataModel)
        {
            if (inkDataModel.InkTree.Root == null)
                return new List<RasterInkStroke>();

            List<RasterInkStroke> dryStrokes = new List<RasterInkStroke>(inkDataModel.Strokes.Count);

            DecodedRasterInkBuilder decodedRasterInkBuilder = new DecodedRasterInkBuilder();

            foreach (var stroke in inkDataModel.Strokes)
            {
                var dryStroke = CreateDryStroke(decodedRasterInkBuilder, stroke, inkDataModel);
                dryStrokes.Add(dryStroke);

                bool res = inkDataModel.SensorData.GetSensorData(dryStroke.SensorDataId, out SensorData sensorData);

                if (res)
                {
                    mSerializer.LoadSensorDataFromModel(inkDataModel, sensorData);
                }
            }

            return dryStrokes;
        }

        private RasterInkStroke CreateDryStroke(DecodedRasterInkBuilder decodedVectorInkBuilder, Stroke stroke, InkModel inkDataModel)
        {
            inkDataModel.Brushes.TryGetBrush(stroke.Style.BrushUri, out Wacom.Ink.Serialization.Model.Brush brush);

            if (brush is Wacom.Ink.Serialization.Model.VectorBrush vectorBrush)
            {
                throw new Exception("This sample does not support serialization of both raster and vector brushes");
            }
            else if (brush is RasterBrush rasterBrush)
            {
                return CreateDryStrokeFromRasterBrush(decodedVectorInkBuilder, rasterBrush, stroke);
            }
            else
            {
                throw new Exception("Brush not recognized");
            }
        }

        private RasterInkStroke CreateDryStrokeFromRasterBrush(DecodedRasterInkBuilder decodedRasterInkBuilder, RasterBrush rasterBrush, Stroke stroke)
        {
            var result = decodedRasterInkBuilder.AddWholePath(stroke.Spline.Data, rasterBrush.Spacing, stroke.Layout);

            List<float> points = new List<float>(result.Addition);

            uint channelMask = (uint)decodedRasterInkBuilder.SplineInterpolator.InterpolatedSplineLayout.ChannelMask;

            ParticleList particleList = new ParticleList(channelMask);
            particleList.Assign(points);

            RasterInkStroke dryStroke = new RasterInkStroke(stroke, rasterBrush, particleList);

            return dryStroke;
        }

        private class DecodedRasterInkBuilder
        {
            #region Fields

            private const int splitCount = 1;

            #endregion

            #region Properties

            public SplineInterpolator SplineInterpolator { get; private set; }

            #endregion

            public ProcessorResult<List<float>> AddWholePath(List<float> path, float spacing, PathPointLayout layout)
            {
                if (path.Count == 0)
                    throw new Exception("Path has no points!");

                SplineInterpolator = new DistanceBasedInterpolator(layout, spacing, splitCount, true, true);

                var iterpolatedPoints = SplineInterpolator.Add(true, true, new Spline(layout.ChannelMask, path), new Spline(layout.ChannelMask));

                return iterpolatedPoints;
            }
        }

        #endregion

    }
}