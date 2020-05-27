using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wacom.Ink;
using Wacom.Ink.Manipulation;
using Wacom.Ink.Serialization;
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

namespace Tutorial_03
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part2 : Page
	{
		#region Fields

		private Color _backgroundColor = Colors.Honeydew;
		private uint? _pointerId;
		private int _updateFromIndex;
		private List<Stroke> _strokes = new List<Stroke>();

		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private StrokeRenderer _strokeRenderer;
		private Layer _backbufferLayer;
		private Layer _strokesLayer;
		private SpeedPathBuilder _pathBuilder;

		#endregion

		public Part2()
		{
			this.InitializeComponent();

			// Attach to events
			this.Loaded += Page_Loaded;
			this.Unloaded += Page_Unloaded;
			Application.Current.Suspending += App_Suspending;
			DisplayInformation.DisplayContentsInvalidated += DisplayInformation_DisplayContentsInvalidated;
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += Part2_BackRequested;

			CreateWacomInkObjects();
		}

		private void Part2_BackRequested(object sender, Windows.UI.Core.BackRequestedEventArgs e)
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
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= Part2_BackRequested;
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
			_backbufferLayer.Dispose();

			// set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// recreate the layers
			InitSizeDependentResources();

			// redraw strokes
			DrawAllStrokes();
		}

		#endregion

		void CreateWacomInkObjects()
		{
			// Create a graphics object
			_graphics = new Graphics();

			// Create a path builder
			_pathBuilder = new SpeedPathBuilder();
			_pathBuilder.SetMovementThreshold(0.1f);
			_pathBuilder.SetNormalizationConfig(100.0f, 4000.0f);
			_pathBuilder.SetPropertyConfig(PropertyName.Width, 2.0f, 10.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);

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
			_strokeRenderer.Init(_graphics, canvasSize, scale);
		}


		void DisposeWacomInkObjects()
		{
			SafeDispose(_pathBuilder);
			SafeDispose(_strokesLayer);
			SafeDispose(_backbufferLayer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

			_pathBuilder = null;
			_strokesLayer = null;
			_backbufferLayer = null;
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

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.Begin, e);

			IntersectStrokes();
		}

		void OnPointerInputMove(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.Move, e);

			IntersectStrokes();
		}

		void OnPointerInputEnd(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			// Reset the stored id and release the pointer capture
			_pointerId = null;
			this.DxPanel.ReleasePointerCapture(e.Pointer);

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.End, e);

			IntersectStrokes();
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
		}

		void AddCurrentPointToPathBuilder(InputPhase phase, PointerRoutedEventArgs e)
		{
			PointerPoint pointerPoint = e.GetCurrentPoint(this.DxPanel);

			Path pathPart = _pathBuilder.AddPoint(phase, pointerPoint);

			if (pathPart.PointsCount > 0)
			{
				int indexOfFirstAffectedPoint;
				_pathBuilder.AddPathPart(pathPart, out indexOfFirstAffectedPoint);

				if (_updateFromIndex == -1)
				{
					_updateFromIndex = indexOfFirstAffectedPoint;
				}
			}
		}

		void IntersectStrokes()
		{
			if (_strokes.Count == 0)
				return;

			if (_updateFromIndex < 0)
				return;

			Path currentPath = _pathBuilder.CurrentPath;

			int numberOfPointsToUpdate = currentPath.PointsCount - _updateFromIndex;
			if (numberOfPointsToUpdate <= 0)
				return;

			bool mustRedrawStrokes = false;

			using (Intersector intersector = new Intersector())
			{
				intersector.SetTargetAsStroke(currentPath, _updateFromIndex, numberOfPointsToUpdate, null);

				_updateFromIndex = -1;

				for (int i = _strokes.Count - 1; i >= 0; i--)
				{
					Stroke s = _strokes[i];

					Rect strokeBounds;
					List<Rect> segmentsBounds = s.GetBounds(out strokeBounds);

					if (segmentsBounds == null)
						continue;

					if (intersector.IsIntersectingTarget(s.Path, 0, s.Path.PointsCount, s.Width, s.Ts, s.Tf, strokeBounds, segmentsBounds))
					{
						_strokes.RemoveAt(i);
						mustRedrawStrokes = true;
					}
				}
			}

			if (mustRedrawStrokes)
			{
				DrawAllStrokes();
			}
		}

		void DrawAllStrokes()
		{
			_renderingContext.SetTarget(_strokesLayer);
			_renderingContext.ClearColor(_backgroundColor);

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

			// copy the strokes layer to the backbuffer layer
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);

			// present
			_graphics.Present();
		}
	}
}
