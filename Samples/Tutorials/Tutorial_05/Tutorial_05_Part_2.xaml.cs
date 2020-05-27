using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Wacom.Ink;
using Wacom.Ink.Smoothing;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
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

namespace Tutorial_05
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part2 : Page
	{
		#region Fields

		private Color _backgroundColor = Colors.Black;
		private uint? _pointerId;
		private int _updateFromIndex;
		private bool _pathFinished;
		private Windows.UI.Xaml.Media.Matrix _imageMatrix = new Windows.UI.Xaml.Media.Matrix(0.8, 0.0, 0.0, 0.8, 50.0, 50.0);

		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private StrokeRenderer _strokeRenderer;
		private Layer _backbufferLayer;
		private Layer _sceneLayer;
		private Layer _imageLayer;
		private Layer _maskLayer;
		private Texture _texture;
		private SpeedPathBuilder _pathBuilder;
		private MultiChannelSmoothener _smoothener;

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
			_backbufferLayer.Dispose();
			_sceneLayer.Dispose();

			// set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// recreate the layers
			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();
			_sceneLayer = _graphics.CreateLayer(canvasSize, scale);
			_maskLayer = _graphics.CreateLayer(canvasSize, scale);
			_strokeRenderer.Init(_graphics, _graphics.Size, _graphics.Scale);

			DrawImage();
		}

		#endregion

		void CreateWacomInkObjects()
		{
			// Create a graphics object
			_graphics = new Graphics();

			// Create a path builder
			_pathBuilder = new SpeedPathBuilder();
			_pathBuilder.SetMovementThreshold(0.1f);

			// Create an object that smooths input data
			_smoothener = new MultiChannelSmoothener(_pathBuilder.PathStride);

			// Create a stroke renderer
			_strokeRenderer = new StrokeRenderer();
			_strokeRenderer.Color = Colors.Red;
			_strokeRenderer.Brush = new SolidColorBrush();
			_strokeRenderer.StrokeWidth = 1.0f;
			_strokeRenderer.UseVariableAlpha = false;
			_strokeRenderer.Ts = 0.0f;
			_strokeRenderer.Tf = 1.0f;
		}

		async Task InitInkRendering()
		{
			_graphics.Initialize(this.DxPanel);
			_renderingContext = _graphics.GetRenderingContext();

			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();
			_sceneLayer = _graphics.CreateLayer(canvasSize, scale);
			_maskLayer = _graphics.CreateLayer(canvasSize, scale);
			_strokeRenderer.Init(_graphics, canvasSize, scale);

			await LoadImage();
		}

		void DisposeWacomInkObjects()
		{
			SafeDispose(_pathBuilder);
			SafeDispose(_smoothener);
			SafeDispose(_sceneLayer);
			SafeDispose(_imageLayer);
			SafeDispose(_texture);
			SafeDispose(_backbufferLayer);
			SafeDispose(_maskLayer);
			SafeDispose(_strokeRenderer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

			_pathBuilder = null;
			_smoothener = null;
			_sceneLayer = null;
			_imageLayer = null;
			_texture = null;
			_backbufferLayer = null;
			_maskLayer = null;
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

			// Clear the stroke renderer
			_strokeRenderer.ResetAndClear();

			// Add the pointer point to the path builder
			AddCurrentPointToPathBuilder(InputPhase.Begin, e);

			// copy the image layer to the backbuffer layer
			_renderingContext.SetTarget(_sceneLayer);
			_renderingContext.ClearColor(_backgroundColor);
			_renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

			DrawImageAndCurrentStroke();
		}

		void OnPointerInputMove(PointerRoutedEventArgs e)
		{
			// Ignore events from other pointers
			if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
				return;

			AddCurrentPointToPathBuilder(InputPhase.Move, e);

			DrawImageAndCurrentStroke();
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

			DrawImage();
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

		void DrawImageAndCurrentStroke()
		{
			if (_updateFromIndex < 0)
				return;

			Path currentPath = _pathBuilder.CurrentPath;

			int numberOfPointsToDraw = currentPath.PointsCount - _updateFromIndex;
			if (numberOfPointsToDraw <= 0)
				return;

			_strokeRenderer.DrawStroke(currentPath, _updateFromIndex, numberOfPointsToDraw, _pathFinished);

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

			// Recompose the scene within the updated area
			Rect updatedRect = _strokeRenderer.UpdatedRect;
			Point destLocation = new Point(updatedRect.X, updatedRect.Y);

			// Draw background and image
			_renderingContext.SetTarget(_sceneLayer, _strokeRenderer.UpdatedRect);
			_renderingContext.ClearColor(_backgroundColor);
			_renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

			// Draw the new part
			_strokeRenderer.BlendStrokeUpdatedAreaInLayer(_sceneLayer, BlendMode.Normal);

			// copy to backbuffer and present
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.DrawLayer(_sceneLayer, null, BlendMode.None);
			_graphics.Present();
		}

		void DrawImage()
		{
			Path currentPath = _pathBuilder.CurrentPath;

			bool drawMask = (currentPath.PointsCount > 0);

			if (drawMask)
			{
				_renderingContext.SetTarget(_maskLayer);
				_renderingContext.ClearColor(_backgroundColor);
				_renderingContext.FillPath(currentPath, Color.FromArgb(255, 255, 255, 255), true);
			}

			// copy the image layer to the backbuffer layer
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.ClearColor(_backgroundColor);
			_renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

			if (drawMask)
			{
				_renderingContext.DrawLayer(_maskLayer, null, BlendMode.MultiplyNoAlpha);
			}

			// present
			_graphics.Present();
		}

		async Task LoadImage()
		{
			await LoadImageTexture();

			Size size = new Size(_texture.Width, _texture.Height);

			_imageLayer = _graphics.CreateLayer(size, _graphics.Scale, _texture);

			DrawImage();
		}

		async Task LoadImageTexture()
		{
			PixelData pixelData = await GetPixelData(new Uri(@"ms-appx:///Assets/image.jpg"));
			_texture = _graphics.CreateTexture(pixelData);
		}

		static async Task<PixelData> GetPixelData(Uri uri)
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
	}
}
