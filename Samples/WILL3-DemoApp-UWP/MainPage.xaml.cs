using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using InkModel = Wacom.Ink.Serialization.Model.InkModel;
using UimCodec = Wacom.Ink.Serialization.UimCodec;


namespace WacomInkDemoUWP
{
	/// <summary>
	/// The main page 
	/// </summary>
	public sealed partial class MainPage : Page
    {
        #region Fields

        private static readonly string s_uimExtension = ".uim";

        private readonly InkPanel m_inkPanel = new InkPanel();

        #endregion

        #region Constructor

        public MainPage()
        {
            Trace.WriteLine("MainPage");
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
		{
			ValidateWacomLicense();

			m_inkPanel.Initialize(swapChainPanel);
			SetInkColor(Colors.Blue);
			SetVectorTool(btnBallPen, "wdt:BallPen");
		}

		private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Dispose();
        }

        private void OnBallPen_Click(object sender, RoutedEventArgs e)
        {
            SetVectorTool((AppBarToggleButton)sender, "wdt:BallPen");
        }

        private void OnFountainPen_Click(object sender, RoutedEventArgs e)
        {
            SetVectorTool((AppBarToggleButton)sender, "wdt:FountainPen");
        }

        private void OnBrush_Click(object sender, RoutedEventArgs e)
        {
            SetVectorTool((AppBarToggleButton)sender, "wdt:Brush");
        }

        private void OnPencil_Click(object sender, RoutedEventArgs e)
        {
            SetRasterTool((AppBarToggleButton)sender, "wdt:Pencil");
        }

        private void OnWaterBrush_Click(object sender, RoutedEventArgs e)
        {
            SetRasterTool((AppBarToggleButton)sender, "wdt:WaterBrush");
        }

        private void OnCrayon_Click(object sender, RoutedEventArgs e)
        {
            SetRasterTool((AppBarToggleButton)sender, "wdt:Crayon");
        }

        private void OnColorSet_Click(object sender, RoutedEventArgs e)
        {
            BtnColorFlyout.Hide();

            SetInkColor(BtnColorPicker.Color);
        }

        private void OnClear_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Clear();			
        }

        private async void OnSave_Click(object sender, RoutedEventArgs e)
        {
            if (m_inkPanel.StrokesCount == 0)
            {
                MessageDialog md = new MessageDialog("No strokes to save!", "Error saving file");
                await md.ShowAsync();
                return;
            }

            FileSavePicker savePicker = new FileSavePicker();
            savePicker.FileTypeChoices.Add("Universal Ink Model", new List<string>() { s_uimExtension });
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = "ink";

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file == null)
            {
                // Operation canceled by user.
                return;
            }

			if (file.FileType != s_uimExtension)
			{
				MessageDialog md = new MessageDialog($"File type '{file.FileType}' is not supported.", "File Save Error");
				await md.ShowAsync();
                return;
			}

			try
			{
				InkModel inkModel = await CreateInkModelAsync();

				// Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
				CachedFileManager.DeferUpdates(file);

				using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
				{
					UimCodec.Encode(inkModel, fileStream.AsStream());

					await fileStream.FlushAsync();
				}

				// Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
				// Completing updates may require Windows to ask for user input.
				var status = await CachedFileManager.CompleteUpdatesAsync(file);

				if (status != FileUpdateStatus.Complete)
				{
					throw new Exception("Error saving file");
				}
			}
			catch (Exception ex)
            {
                MessageDialog md = new MessageDialog(ex.Message, "Exception");
                await md.ShowAsync();
            }
        }

		private async void OnLoad_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add(s_uimExtension);

            StorageFile file = await picker.PickSingleFileAsync();

            if ((file != null) && (file.FileType == s_uimExtension))
            {
                try
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        InkModel inkModel = UimCodec.Decode(stream.AsStream());

                        LoadInkModel(inkModel);
                    }
                }
                catch (Exception ex)
                {
                    MessageDialog md = new MessageDialog(ex.Message, "Error loading file.");
                    await md.ShowAsync();
                }
            }
        }

		private async Task<InkModel> CreateInkModelAsync()
		{
			try
			{
				DisableUI();

				return await m_inkPanel.CreateUniversalInkModelAsync();
			}
			finally
			{
				EnableUI();
			}
		}

		private void LoadInkModel(InkModel inkModel)
		{
			try
			{
				DisableUI();

				m_inkPanel.LoadStrokesFromModel(inkModel);
			}
			finally
			{
				EnableUI();
			}
		}

		private void EnableUI()
		{
			appCommandBar.IsEnabled = true;
			swapChainPanel.Visibility = Visibility.Visible;
			progressRing.Visibility = Visibility.Collapsed;
			progressRing.IsActive = false;
		}

		private void DisableUI()
		{
			appCommandBar.IsEnabled = false;
			swapChainPanel.Visibility = Visibility.Collapsed;
			progressRing.Visibility = Visibility.Visible;
			progressRing.IsActive = true;
		}

		private void OnManipulatePart_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.SetOperationMode(OperationMode.SelectStrokePart);
            m_inkPanel.SetPartialStrokeSelectorTool("wdt:CircleSelector");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnManipulateWhole_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.SetOperationMode(OperationMode.SelectWholeStroke);
            m_inkPanel.SetWholeStrokeSelectorTool("wdt:CircleSelector");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnErasePart_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.SetOperationMode(OperationMode.EraseStrokePart);
            m_inkPanel.SetPartialStrokeEraserTool("wdt:CircleEraser");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnEraseWhole_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.SetOperationMode(OperationMode.EraseWholeStroke);
            m_inkPanel.SetWholeStrokeEraserTool("wdt:CircleEraser");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void SetVectorTool(AppBarToggleButton btn, string toolName)
        {
            EnableManipulation(true);
            m_inkPanel.SetOperationMode(OperationMode.VectorDrawing);
            m_inkPanel.SetVectorTool(toolName);
            ToggleBrushButton(btn);
        }

        private void SetRasterTool(AppBarToggleButton btn, string toolName)
        {
            EnableManipulation(true);
            m_inkPanel.SetOperationMode(OperationMode.RasterDrawing);
            m_inkPanel.SetRasterTool(toolName);
            ToggleBrushButton(btn);
        }

		private static void ValidateWacomLicense()
		{
			// We read our license string from the WacomLicense.txt file in the Assets folder

			try
			{
				Trace.WriteLine("Looking for license");
				var license = Task.Run(async () =>
				{
					Trace.WriteLine($"GetFileAsync");

					StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/WacomLicense.txt"));

					Trace.WriteLine($"ReadTextAsync {file.Path}");
					string text = await Windows.Storage.FileIO.ReadTextAsync(file);
					Trace.WriteLine($"text {text}");
					return text.Trim();
				}).Result;

				Trace.WriteLine($"Get license {license}");
				if (!string.IsNullOrEmpty(license))
				{
					Wacom.Licensing.LicenseValidator.Instance.SetLicense(license);
				}
			}
			catch (Exception ex)
			{
				// Assume error finding or reading license file
				Trace.WriteLine($"Exception {ex}");
			}

			if (!Wacom.Licensing.LicenseValidator.Instance.HasLicense)
			{
				MessageDialog md = new MessageDialog("WILL SDK for Ink is not licensed. Some functionality is not enabled",
					"Not Licensed");
				Task.Run(() => md.ShowAsync());
			}
		}

		private void EnableManipulation(bool enable)
        {
            btnSelect.IsEnabled = enable;
            btnSelectWhole.IsEnabled = enable;
            btnErase.IsEnabled = enable;
            btnEraseWhole.IsEnabled = enable;
        }

        private AppBarToggleButton mCurrentBrushBtn = null;

        private void ToggleBrushButton(AppBarToggleButton btn)
        {
            if (mCurrentBrushBtn != null)
                mCurrentBrushBtn.IsChecked = false;

            mCurrentBrushBtn = btn;
            mCurrentBrushBtn.IsChecked = true;
        }

        private void SetInkColor(Color color)
        {
            BtnColorIcon.Foreground = new SolidColorBrush(color);

            m_inkPanel.SetInkColor(color);
        }
    }
}
