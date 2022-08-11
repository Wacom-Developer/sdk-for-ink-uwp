using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Wacom.Ink.Serialization;
using Wacom.Ink.Serialization.Model;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace WacomInkDemoUWP
{
    /// <summary>
    /// The main page 
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Fields
        public static readonly string PROTO_EXT = ".proto";
        public static readonly string UIM_EXT = ".uim";
        public static readonly string JSON_EXT = ".json";

        private readonly InkPanel m_inkPanel = new InkPanel();

        #endregion

        #region Constructor

        public MainPage()
        {
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #endregion

        #region Event Handlers

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Initialize(swapChainPanel);
            SetInkColor(Colors.Blue);
            ToggleBrushButton(btnPen);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Dispose();
        }


        #endregion

        private void OnPen_Click(object sender, RoutedEventArgs e)
        {
            SetVectorTool((AppBarToggleButton)sender, "wdt:Pen");
        }

        private void OnFelt_Click(object sender, RoutedEventArgs e)
        {
            SetVectorTool((AppBarToggleButton)sender, "wdt:Felt");
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
            int count = m_inkPanel.Model.Strokes.Count;

            if (count == 0)
            {
                MessageDialog md = new MessageDialog("No strokes to save!", "Error saving file");
                await md.ShowAsync();
                return;
            }

            FileSavePicker savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("Universal Ink Model", new List<string>() { UIM_EXT });
            savePicker.FileTypeChoices.Add("Json Ink Model", new List<string>() { JSON_EXT });
            savePicker.SuggestedFileName = "ink";

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file == null)
            {
                // Operation canceled by user.
                return;
            }

            if (file.FileType != UIM_EXT &&
                file.FileType != JSON_EXT)
            {
                MessageDialog md = new MessageDialog($"Unknown file type '{file.FileType}'!", "Error saving file");
                await md.ShowAsync();
                return;
            }

            InkModel inkModel = m_inkPanel.Model.BuildUniversalInkModelFromCanvasStrokes();
            await m_inkPanel.Model.BuildUIMBrushesFromAppBrushes(inkModel);

            // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
            CachedFileManager.DeferUpdates(file);

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                if (file.FileType == UIM_EXT)
                {
                    UimCodec.Encode(inkModel, stream.AsStream());
                }
                else if (file.FileType == JSON_EXT)
                {
                    IReadOnlyList<string> jsons = null;

                    using (MemoryStream uimOutputStream = new MemoryStream())
                    {
                        UimCodec.Encode(inkModel, uimOutputStream);

                        uimOutputStream.Seek(0, SeekOrigin.Begin);

                        UimDecoder decoder = new UimDecoder();
                        jsons = decoder.ConvertToJson(uimOutputStream);
                    }

                    if (jsons != null && jsons.Count > 0)
                    {
                        using (TextWriter tw = new StreamWriter(stream.AsStream(), Encoding.UTF8, 4096, true))
                        {
                            foreach (var json in jsons)
                            {
                                tw.Write(json);
                            }
                        }
                    }
                }

                await stream.FlushAsync();
            }

            // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
            // Completing updates may require Windows to ask for user input.
            var status = await CachedFileManager.CompleteUpdatesAsync(file);

            if (status != FileUpdateStatus.Complete)
            {
                MessageDialog md = new MessageDialog("File could not be saved!", "Error saving file");
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
            picker.FileTypeFilter.Add(UIM_EXT);

            StorageFile file = await picker.PickSingleFileAsync();

            if ((file != null) && (file.FileType == UIM_EXT))
            {
                try
                {
                    using (var stream = await file.OpenReadAsync())
                    {
                        InkModel inkModel = UimCodec.Decode(stream.AsStream());

                        await m_inkPanel.LoadStrokesFromModel(inkModel);
                    }

                    m_inkPanel.RebuildAndRepaintStrokesAndOverlay();
                }
                catch (Exception ex)
                {
                    MessageDialog md = new MessageDialog(ex.Message, "Error loading file.");
                    await md.ShowAsync();
                }
            }
        }

        private void OnManipulatePart_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Controller.SetOperationMode(OperationMode.SelectStrokePart);
            m_inkPanel.SetPartialStrokeSelectorTool("wdt:CircleSelector");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnManipulateWhole_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Controller.SetOperationMode(OperationMode.SelectStrokeWhole);
            m_inkPanel.SetWholeStrokeSelectorTool("wdt:CircleSelector");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnErasePart_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Controller.SetOperationMode(OperationMode.EraseStrokePart);
            m_inkPanel.SetPartialStrokeEraserTool("wdt:CircleEraser");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void OnEraseWhole_Click(object sender, RoutedEventArgs e)
        {
            m_inkPanel.Controller.SetOperationMode(OperationMode.EraseWholeStroke);
            m_inkPanel.SetWholeStrokeEraserTool("wdt:CircleEraser");
            ToggleBrushButton((AppBarToggleButton)sender);
        }

        private void SetVectorTool(AppBarToggleButton btn, string toolName)
        {
            EnableManipulation(true);
            m_inkPanel.Controller.SetOperationMode(OperationMode.VectorDrawing);
            m_inkPanel.SetVectorTool(toolName);
            ToggleBrushButton(btn);
        }

        private void SetRasterTool(AppBarToggleButton btn, string toolName)
        {
            EnableManipulation(true);
            m_inkPanel.Controller.SetOperationMode(OperationMode.RasterDrawing);
            m_inkPanel.SetRasterTool(toolName);
            ToggleBrushButton(btn);
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

        private void SetInkColor(MediaColor color)
        {
            BtnColorIcon.Foreground = new SolidColorBrush(color);
            m_inkPanel.Controller.DrawVectorStrokeOp.Color = color;
            m_inkPanel.Controller.DrawRasterStrokeOp.Color = color;
        }

    }

}
