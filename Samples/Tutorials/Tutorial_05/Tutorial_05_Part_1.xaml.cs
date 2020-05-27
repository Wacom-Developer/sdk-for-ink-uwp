using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Wacom.Ink;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Tutorial_05
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part1 : Page
	{
		#region Fields

		private Color _backgroundColor = Colors.Black;
		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private Layer _backbufferLayer;
		private Layer _imageLayer;
		private Texture _texture;
		private Matrix _imageMatrix = new Matrix(0.8, 0.0, 0.0, 0.8, 50.0, 50.0);

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
		}

		void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			DisposeWacomInkObjects();

			// Detach from swap chain panel events
			this.DxPanel.SizeChanged -= DxPanel_SizeChanged;
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

		void DxPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_renderingContext == null)
				return;

			// release existing layers
			_renderingContext.SetTarget(null);
			_backbufferLayer.Dispose();

			// set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// recreate the layers
			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();

			DrawImage();
		}

		#endregion

		void CreateWacomInkObjects()
		{
			// Create a graphics object
			_graphics = new Graphics();
		}

		async Task InitInkRendering()
		{
			_graphics.Initialize(this.DxPanel);
			_renderingContext = _graphics.GetRenderingContext();

			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();

			await LoadImage();
		}

		void DisposeWacomInkObjects()
		{
			SafeDispose(_backbufferLayer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

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

		void DrawImage()
		{
			// copy the image layer to the backbuffer layer
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.ClearColor(_backgroundColor);
			_renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

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
