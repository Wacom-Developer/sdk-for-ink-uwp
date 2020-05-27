using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wacom.Ink;
using Wacom.Ink.Manipulation;
using Wacom.Ink.Serialization;
using Wacom.Ink.Smoothing;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Tutorial_04
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part1 : Page
	{
		#region Fields

		private Color _backgroundColor = Colors.Honeydew;
		private uint? _pointerId;
		private int _updateFromIndex;
		private bool _pathFinished;
		private List<Stroke> _strokes = new List<Stroke>();

		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private StrokeRenderer _strokeRenderer;
		private Layer _backbufferLayer;
		private Layer _strokesLayer;
		private Layer _sceneLayer;
		private SpeedPathBuilder _pathBuilder;
		private MultiChannelSmoothener _smoothener;

		#endregion

		public Part1()
		{
			this.InitializeComponent();

			// Attach to events
			this.Loaded += Page_Loaded;
			this.Unloaded += Page_Unloaded;
			Application.Current.Suspending += App_Suspending;
			DisplayInformation.DisplayContentsInvalidated += DisplayInformation_DisplayContentsInvalidated;
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += Part1_BackRequested;

			CreateWacomInkObjects();
		}

		private void Part1_BackRequested(object sender, Windows.UI.Core.BackRequestedEventArgs e)
		{
			Frame rootFrame = Window.Current.Content as Frame;
			if (rootFrame == null)
				return;

			// Navigate back if possible, and if the event has not 
			// already been handled .
			if (rootFrame.CanGoBack && e.Handled == false)
			{
				e.Handled = true;
				rootFrame.GoBack();
			}
		}
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			Frame rootFrame = Window.Current.Content as Frame;

			if (rootFrame.CanGoBack)
			{
				// Show UI in title bar if opted-in and in-app backstack is not empty.
				SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
					AppViewBackButtonVisibility.Visible;
			}
		}
		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= Part1_BackRequested;
		}

		async void Page_Loaded(object sender, RoutedEventArgs e)
		{
			await InitInkRendering();

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
			_strokesLayer.Dispose();
			_sceneLayer.Dispose();
			_backbufferLayer.Dispose();

			// set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// recreate the layers
			InitSizeDependentResources();

			// redraw the strokes
			DrawAllStrokes();
			Present();
		}

		#endregion

		void CreateWacomInkObjects()
		{
			// Create a graphics object
			_graphics = new Graphics();

			// Create a path builder for constant stroke width
			_pathBuilder = new SpeedPathBuilder();
			_pathBuilder.SetMovementThreshold(0.1f);

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

		async Task InitInkRendering()
		{
			_graphics.Initialize(this.DxPanel);
			_renderingContext = _graphics.GetRenderingContext();

			InitSizeDependentResources();

			await LoadStrokes();
		}

		void InitSizeDependentResources()
		{
			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();
			_strokesLayer = _graphics.CreateLayer(canvasSize, scale);
			_sceneLayer = _graphics.CreateLayer(canvasSize, scale);
			_strokeRenderer.Init(_graphics, canvasSize, scale);
		}

		void DisposeWacomInkObjects()
		{
			SafeDispose(_pathBuilder);
			SafeDispose(_smoothener);
			SafeDispose(_strokesLayer);
			SafeDispose(_backbufferLayer);
			SafeDispose(_sceneLayer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

			_pathBuilder = null;
			_smoothener = null;
			_strokesLayer = null;
			_backbufferLayer = null;
			_sceneLayer = null;
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

			// Reset the stroke renderer and prepare it for selector stroke rendering
			_strokeRenderer.ResetAndClear();
			_strokeRenderer.Color = Colors.Red;
			_strokeRenderer.StrokeWidth = 1.0f;
			_strokeRenderer.Ts = 0.0f;
			_strokeRenderer.Tf = 1.0f;

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.Begin, e);

			DrawCurrentStroke();
		}

		void OnPointerInputMove(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			AddCurrentPointToPathBuilder(InputPhase.Move, e);

			DrawCurrentStroke();
		}

		void OnPointerInputEnd(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			// Reset the stored id and release the pointer capture
			_pointerId = null;
			this.DxPanel.ReleasePointerCapture(e.Pointer);

			AddCurrentPointToPathBuilder(InputPhase.End, e);

			_pathFinished = true;
			_updateFromIndex = 0;

			if (IntersectStrokes())
			{
				DrawAllStrokes();
			}
			else
			{
				_renderingContext.SetTarget(_sceneLayer);
				_renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);
			}

			Present();
		}

		async Task LoadStrokes()
		{
			StorageFile storageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(@"ms-appx:///Assets/strokes.bin"));

			if (storageFile != null)
			{
				IBuffer buffer = await FileIO.ReadBufferAsync(storageFile);

				using (StrokeDecoder decoder = new StrokeDecoder(buffer))
				{
					while (decoder.MoveNext())
					{
						StrokeData strokeData = decoder.DecodeCurrent();
						_strokes.Add(new Stroke(strokeData));
					}
				}
			}

			DrawAllStrokes();
			Present();
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

		void DrawCurrentStroke()
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
			_renderingContext.DrawLayerAtPoint(_strokesLayer, updatedRect, destLocation, BlendMode.None);

			// draw the new part
			_strokeRenderer.BlendStrokeUpdatedAreaInLayer(_sceneLayer, BlendMode.Normal);

			// copy to backbuffer and present
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.DrawLayer(_sceneLayer, null, BlendMode.None);
			_graphics.Present();
		}

		void DrawAllStrokes()
		{
			_renderingContext.SetTarget(_strokesLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_strokeRenderer.StrokeWidth = null;

			for (int i = 0; i < _strokes.Count; i++)
			{
				Stroke s = _strokes[i];

				_strokeRenderer.Color = s.Color;
				_strokeRenderer.Ts = s.Ts;
				_strokeRenderer.Tf = s.Tf;

				_strokeRenderer.ResetAndClear();
				_strokeRenderer.DrawStroke(s.Path, 0, s.Path.PointsCount, true);
				_strokeRenderer.BlendStrokeInLayer(_strokesLayer, BlendMode.Normal);
			}

			_renderingContext.SetTarget(_sceneLayer);
			_renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);
		}

		void Present()
		{
			// copy the strokes layer to the backbuffer layer
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);

			// present
			_graphics.Present();
		}

		bool IntersectStrokes()
		{
			if (_strokes.Count == 0)
				return false;

			Path currentPath = _pathBuilder.CurrentPath;

			if (currentPath.PointsCount <= 0)
				return false;

			bool mustRedrawStrokes = false;

			using (Intersector intersector = new Intersector())
			{
				intersector.SetTargetAsClosedPath(currentPath, 0, currentPath.PointsCount);

				for (int i = _strokes.Count - 1; i >= 0; i--)
				{
					Stroke s = _strokes[i];

					Rect strokeBounds;
					List<Rect> segmentsBounds = s.GetBounds(out strokeBounds);

					if (segmentsBounds == null)
						continue;

					if (intersector.IsIntersectingTarget(s.Path, 0, s.Path.PointsCount, s.Width, s.Ts, s.Tf, strokeBounds, segmentsBounds))
					{
						s.InvertColor();
						mustRedrawStrokes = true;
					}
				}
			}

			return mustRedrawStrokes;
		}
	}
}
