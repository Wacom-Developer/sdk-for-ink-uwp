using System;
using System.Collections.Generic;
using Wacom.Ink;
using Wacom.Ink.Smoothing;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Tutorial_06
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part1 : Page
	{
		#region Fields

		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private StrokeRenderer _strokeRenderer;
		private Layer _backbufferLayer;
		private Layer _sceneLayer;
		private Layer _allStrokesLayer;
		private PressurePathBuilder _pathBuilder;
		private MultiChannelSmoothener _smoothener;

		private Random _rand = new Random();
		private Color _backgroundColor = Colors.Honeydew;
		private uint? _pointerId;
		private int _updateFromIndex;
		private bool _pathFinished;
		private List<Stroke> _strokes = new List<Stroke>();
		private Dictionary<ulong, Color> _penIdToColorMap = new Dictionary<ulong, Color>();

		#endregion

		public Part1()
		{
			this.InitializeComponent();

			// Attach to events
			this.Loaded += Page_Loaded;
			this.Unloaded += Page_Unloaded;
			Application.Current.Suspending += App_Suspending;
			DisplayInformation.DisplayContentsInvalidated += DisplayInformation_DisplayContentsInvalidated;

			CreateWacomInkObjects();
		}

		void Page_Loaded(object sender, RoutedEventArgs e)
		{
			InitInkRendering();

			// Attach to swap chain panel events
			this.DxPanel.SizeChanged += DxPanel_SizeChanged;
			this.DxPanel.PointerPressed += DxPanel_PointerPressed;
			this.DxPanel.PointerMoved += DxPanel_PointerMoved;
			this.DxPanel.PointerReleased += DxPanel_PointerReleased;
			this.DxPanel.PointerCaptureLost += DxPanel_PointerCaptureLost;
			this.DxPanel.PointerCanceled += DxPanel_PointerCanceled;
		}

		void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			DisposeWacomInkObjects();

			// Detach from swap chain panel events
			this.DxPanel.SizeChanged -= DxPanel_SizeChanged;
			this.DxPanel.PointerPressed -= DxPanel_PointerPressed;
			this.DxPanel.PointerMoved -= DxPanel_PointerMoved;
			this.DxPanel.PointerReleased -= DxPanel_PointerReleased;
			this.DxPanel.PointerCaptureLost -= DxPanel_PointerCaptureLost;
			this.DxPanel.PointerCanceled -= DxPanel_PointerCanceled;
		}

		void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
		{
			if (_graphics != null)
			{
				_graphics.Trim();
			}
		}

		void DisplayInformation_DisplayContentsInvalidated(DisplayInformation sender, object args)
		{
			if (_graphics != null)
			{
				_graphics.ValidateDevice();
			}
		}

		#region DxPanel Event Handlers

		void DxPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			OnPointerInputBegin(e);
		}

		void DxPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
		{
			OnPointerInputMove(e);
		}

		void DxPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			OnPointerInputEnd(e);
		}

		void DxPanel_PointerCanceled(object sender, PointerRoutedEventArgs e)
		{
			OnPointerInputEnd(e);
		}

		void DxPanel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
		{
			OnPointerInputEnd(e);
		}

		void DxPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_renderingContext == null)
				return;

			// release existing layers
			_renderingContext.SetTarget(null);
			_strokeRenderer.Deinit();
			_sceneLayer.Dispose();
			_allStrokesLayer.Dispose();
			_backbufferLayer.Dispose();

			// set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// recreate the layers
			InitSizeDependentResources();
		}

		#endregion

		#region Buttons Event Handlers

		void ButtonClear_Clicked(object sender, RoutedEventArgs e)
		{
			// clear the strokes collection
			_strokes.Clear();

			// clear the stroke renderer
			_strokeRenderer.ResetAndClear();

			// clear the scene layer
			_renderingContext.SetTarget(_sceneLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_renderingContext.SetTarget(_allStrokesLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_graphics.Present();
		}

		#endregion

		void CreateWacomInkObjects()
		{
			// Create a graphics object
			_graphics = new Graphics();

			// Create a path builder
			_pathBuilder = new PressurePathBuilder();
			_pathBuilder.SetMovementThreshold(0.1f);
			_pathBuilder.SetNormalizationConfig(0.0f, 1.0f);
			_pathBuilder.SetPropertyConfig(PropertyName.Width, 10.0f, 40.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);
			_pathBuilder.SetPropertyConfig(PropertyName.Alpha, 0.08f, 0.9f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);

			// Create an object that smooths input data
			_smoothener = new MultiChannelSmoothener(_pathBuilder.PathStride);

			// Create a stroke renderer
			_strokeRenderer = new StrokeRenderer();
			_strokeRenderer.Brush = new SolidColorBrush();
			_strokeRenderer.StrokeWidth = null;
			_strokeRenderer.UseVariableAlpha = false;
			_strokeRenderer.Ts = 0.0f;
			_strokeRenderer.Tf = 1.0f;
		}

		void InitInkRendering()
		{
			_graphics.Initialize(this.DxPanel);
			_renderingContext = _graphics.GetRenderingContext();

			InitSizeDependentResources();
		}

		void InitSizeDependentResources()
		{
			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();
			_sceneLayer = _graphics.CreateLayer(canvasSize, scale);
			_allStrokesLayer = _graphics.CreateLayer(canvasSize, scale);
			_strokeRenderer.Init(_graphics, canvasSize, scale);

			_renderingContext.SetTarget(_allStrokesLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_renderingContext.SetTarget(_sceneLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_graphics.Present();
		}

		void DisposeWacomInkObjects()
		{
			SafeDispose(_pathBuilder);
			SafeDispose(_smoothener);
			SafeDispose(_sceneLayer);
			SafeDispose(_allStrokesLayer);
			SafeDispose(_backbufferLayer);
			SafeDispose(_strokeRenderer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

			_pathBuilder = null;
			_smoothener = null;
			_sceneLayer = null;
			_allStrokesLayer = null;
			_backbufferLayer = null;
			_strokeRenderer = null;
			_renderingContext = null;
			_graphics = null;
		}

		void SafeDispose(object obj)
		{
			IDisposable disposable = obj as IDisposable;

			if (disposable != null)
			{
				disposable.Dispose();
			}
		}

		void OnPointerInputBegin(PointerRoutedEventArgs e)
		{
			// If currently there is an unfinished stroke - do not interrupt it
			if (_pointerId.HasValue)
				return;

			// Capture the pointer and store its Id
			this.DxPanel.CapturePointer(e.Pointer);
			_pointerId = e.Pointer.PointerId;

			// Reset the state related to path building
			_updateFromIndex = -1;
			_pathFinished = false;

			// Reset the smoothener
			_smoothener.Reset();

			// Reset the stroke renderer
			_strokeRenderer.ResetAndClear();

			// Get a unique Id for the pen and associate it with a color
			ulong uid = PenIdHelper.GetUniquePenId(e.GetCurrentPoint(this.DxPanel));

			PenIdTextBlock.Text = uid.ToString();

			Color color;
			if (!_penIdToColorMap.TryGetValue(uid, out color))
			{
				color = GetRandomColor();
				_penIdToColorMap[uid] = color;
			}

			// Set a random color for the new stroke
			_strokeRenderer.Color = color;

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.Begin, e);

			// Draw the scene
			Render();
		}

		void OnPointerInputMove(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			AddCurrentPointToPathBuilder(InputPhase.Move, e);

			// Draw the scene
			Render();
		}

		void OnPointerInputEnd(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			PenIdTextBlock.Text = "";

			// Reset the stored id and release the pointer capture
			_pointerId = null;
			this.DxPanel.ReleasePointerCapture(e.Pointer);

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.End, e);

			_pathFinished = true;

			// Draw the scene
			Render();

			// Blend the current stroke into the current stroke layer
			_strokeRenderer.BlendStrokeInLayer(_allStrokesLayer, BlendMode.Normal);

			// Store the current stroke
			_strokes.Add(new Stroke(_pathBuilder.CurrentPath, _strokeRenderer.Color));
		}

		Color GetRandomColor()
		{
			// return a random color
			int red = _rand.Next(0, 255);
			int green = _rand.Next(0, 255);
			int maxBlue = ((red + green) > 460) ? 200 : 255;
			int blue = _rand.Next(0, maxBlue);

			return Color.FromArgb((byte)255, (byte)red, (byte)green, (byte)blue);
		}

		void AddCurrentPointToPathBuilder(InputPhase phase, PointerRoutedEventArgs e)
		{
			PointerPoint pointerPoint = e.GetCurrentPoint(this.DxPanel);

			Path pathPart = _pathBuilder.AddPoint(phase, pointerPoint);

			if (pathPart.PointsCount > 0)
			{
				_smoothener.Smooth(pathPart, phase == InputPhase.End);

				int indexOfFirstAffectedPoint;
				_pathBuilder.AddPathPart(pathPart, out indexOfFirstAffectedPoint);

				if (_updateFromIndex == -1)
				{
					_updateFromIndex = indexOfFirstAffectedPoint;
				}
			}
		}

		void Render()
		{
			if (_updateFromIndex < 0)
				return;

			Path currentPath = _pathBuilder.CurrentPath;

			int numberOfPointsToDraw = currentPath.PointsCount - _updateFromIndex;
			if (numberOfPointsToDraw <= 0)
				return;

			_strokeRenderer.DrawStroke(currentPath, _updateFromIndex, numberOfPointsToDraw, _pathFinished);

			// reset the starting index
			_updateFromIndex = -1;

			// draw preliminary path
			if (!_pathFinished)
			{
				Path prelimPathPart = _pathBuilder.CreatePreliminaryPath();

				if (prelimPathPart.PointsCount > 0)
				{
					_smoothener.Smooth(prelimPathPart, true);

					Path preliminaryPath = _pathBuilder.FinishPreliminaryPath(prelimPathPart);

					_strokeRenderer.DrawPreliminaryStroke(preliminaryPath, 0, preliminaryPath.PointsCount);
				}
			}

			// recompose the scene within the updated area
			Rect updatedRect = _strokeRenderer.UpdatedRect;
			Point destLocation = new Point(updatedRect.X, updatedRect.Y);

			// draw background and previous strokes
			_renderingContext.SetTarget(_sceneLayer);
			_renderingContext.DrawLayerAtPoint(_allStrokesLayer, updatedRect, destLocation, BlendMode.None);

			// draw the new part
			_strokeRenderer.BlendStrokeUpdatedAreaInLayer(_sceneLayer, BlendMode.Normal);

			// copy to backbuffer and present
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.DrawLayer(_sceneLayer, null, BlendMode.None);
			_graphics.Present();
		}
	}
}
