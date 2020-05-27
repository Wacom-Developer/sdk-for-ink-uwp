using System;
using Wacom.Ink;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Tutorial_01
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Part1 : Page
	{
		#region Fields

		private Color _backgroundColor = Colors.Honeydew;

		private Graphics _graphics;
		private RenderingContext _renderingContext;
		private StrokeRenderer _strokeRenderer;
		private Layer _backbufferLayer;

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

		void Page_Loaded(object sender, RoutedEventArgs e)
		{
			// Create an inking graphics object and associate it with the DX panel
			_graphics = new Graphics();
			_graphics.Initialize(this.DxPanel);

			// Obtain the rendering context
			_renderingContext = _graphics.GetRenderingContext();

			// Create a layer associated with the DirectX backbuffer
			_backbufferLayer = _graphics.CreateBackbufferLayer();

			// Create a stroke renderer
			_strokeRenderer = new StrokeRenderer();
			_strokeRenderer.Init(_graphics, _graphics.Size, _graphics.Scale);
			_strokeRenderer.Brush = new SolidColorBrush();
			_strokeRenderer.StrokeWidth = 4;
			_strokeRenderer.Color = Colors.Red;
			_strokeRenderer.UseVariableAlpha = false;
			_strokeRenderer.Ts = 0.0f;
			_strokeRenderer.Tf = 1.0f;

			// Draw a static stroke
			DrawStaticStroke();

			// Attach to swap chain panel events
			this.DxPanel.SizeChanged += DxPanel_SizeChanged;
		}

		void Page_Unloaded(object sender, RoutedEventArgs e)
		{
			SafeDispose(_backbufferLayer);
			SafeDispose(_renderingContext);
			SafeDispose(_graphics);

			_backbufferLayer = null;
			_renderingContext = null;
			_graphics = null;

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

		void DxPanel_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_renderingContext == null)
				return;

			// Release existing layers
			_renderingContext.SetTarget(null);
			_strokeRenderer.Deinit();
			_backbufferLayer.Dispose();

			// Set the new size
			_graphics.SetLogicalSize(e.NewSize);

			// Recreate the layers
			Size canvasSize = _graphics.Size;
			float scale = _graphics.Scale;

			_backbufferLayer = _graphics.CreateBackbufferLayer();

			_strokeRenderer.Init(_graphics, _graphics.Size, _graphics.Scale);

			DrawStaticStroke();
		}

		void DrawStaticStroke()
		{
			float[] points = new float[] { 100, 120, 200, 210, 400, 120, 500, 400 };

			Path path = new Path(points, 2, PathFormat.XY);

			_strokeRenderer.ResetAndClear();
			_strokeRenderer.DrawStroke(path, 0, path.PointsCount, true);

			// Clear the backbuffer layer
			_renderingContext.SetTarget(_backbufferLayer);
			_renderingContext.ClearColor(_backgroundColor);

			_strokeRenderer.BlendStrokeInLayer(_backbufferLayer, BlendMode.Normal);

			// Present the backbuffer
			_graphics.Present();
		}

		void SafeDispose(object obj)
		{
			IDisposable disposable = obj as IDisposable;

			if (disposable != null)
			{
				disposable.Dispose();
			}
		}
	}
}
