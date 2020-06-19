using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using Wacom.Ink.Serialization;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    /// <summary>
    /// The main page 
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Fields

        private Renderer m_renderer;
        private MediaColor mBrushColor;

        public MediaColor BrushColor {
            get => mBrushColor;
            set
            {
                mBrushColor = value;
                m_renderer.StrokeHandler.BrushColor = value;
                BtnColorPicker.Color = value;
                BtnColorIcon.Foreground = new SolidColorBrush(value);
            }
        }

        #endregion

        #region Constructor

        public MainPage()
        {
            this.InitializeComponent();
        }

        #endregion

        #region Event Handlers

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            m_renderer = new Renderer(swapChainPanel, VectorBrushStyle.Pen, BrushColor);
            BrushColor = Colors.Blue;
            ToggleBrushButton(btnPen);
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            m_renderer.StopProcessingInput();
            m_renderer.Dispose();
            m_renderer = null;
        }

        #endregion

        private void OnPen_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(VectorBrushStyle.Pen, (AppBarToggleButton)sender);
        }

        private void OnFelt_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(VectorBrushStyle.Felt, (AppBarToggleButton)sender);
        }

        private void OnBrush_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(VectorBrushStyle.Brush, (AppBarToggleButton)sender);
        }

        private void OnPencil_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(RasterBrushStyle.Pencil, (AppBarToggleButton)sender);
        }

        private void OnWaterBrush_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(RasterBrushStyle.WaterBrush, (AppBarToggleButton)sender);
        }

        private void OnCrayon_Click(object sender, RoutedEventArgs e)
        {
            CheckControlType(RasterBrushStyle.Crayon, (AppBarToggleButton)sender);
        }

        private void CheckControlType(VectorBrushStyle brushStyle, AppBarToggleButton btn)
        {
            btnSelect.IsEnabled = true;
            m_renderer.SetHandler(brushStyle, BrushColor);
            ToggleBrushButton(btn);
        }

        private void CheckControlType(RasterBrushStyle brushStyle, AppBarToggleButton btn)
        {
            btnSelect.IsEnabled = false;
            m_renderer.SetHandler(brushStyle, BrushColor);
            ToggleBrushButton(btn);
        }

        private AppBarToggleButton mCurrentBrushBtn = null;
        private AppBarToggleButton mSavedButton = null;

        private void ToggleBrushButton(AppBarToggleButton btn)
        {
            if (mCurrentBrushBtn != null)
                mCurrentBrushBtn.IsChecked = false;
            mCurrentBrushBtn = btn;
            mCurrentBrushBtn.IsChecked = true;
        }

        private void OnColorSet_Click(object sender, RoutedEventArgs e)
        {
            BrushColor = BtnColorPicker.Color;
            BtnColorIcon.Foreground = new SolidColorBrush(BrushColor);
            BtnColorFlyout.Hide();
        }

        private void OnClear_Click(object sender, RoutedEventArgs e)
        {
            m_renderer.ClearStrokes();
        }

        private async void OnSave_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            savePicker.FileTypeChoices.Add("WILL3 file", new List<string>() { ".uim" });
            savePicker.SuggestedFileName = "InkModel";

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {

                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);

                // Write to file
                Will3Codec will3Codec = new Will3Codec();
                await FileIO.WriteBytesAsync(file, will3Codec.Encode(m_renderer.StrokeHandler.Serialize()));

                // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);

                if (status != FileUpdateStatus.Complete)
                {
                    MessageDialog md = new MessageDialog("File could not be saved!", "Error saving file");
                    await md.ShowAsync();
                }
            }

        }

        private async void OnLoad_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".uim");

            StorageFile file = await picker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    IBuffer fileBuffer = await FileIO.ReadBufferAsync(file);

                    Will3Codec will3Codec = new Will3Codec();
                    var inkDocument = will3Codec.Decode(fileBuffer.ToArray());

                    m_renderer.ClearStrokes();

                    if (inkDocument.Brushes.RasterBrushes.Count > 0 && inkDocument.Brushes.VectorBrushes.Count > 0)
                    {
                        throw new NotImplementedException("This sample does not support serialization of both raster and vector brushes");
                    }
                    else if (inkDocument.Brushes.RasterBrushes.Count > 0)
                    {
                        CheckControlType(RasterBrushStyle.Pencil, btnPencil);
                        m_renderer.LoadInk(inkDocument);
                    }
                    else if (inkDocument.Brushes.VectorBrushes.Count > 0)
                    {
                        CheckControlType(VectorBrushStyle.Pen, btnPen);
                        m_renderer.LoadInk(inkDocument);
                    }

                    m_renderer.RedrawAllStrokes(null, null);
                }
                catch (Exception ex)
                {
                    MessageDialog md = new MessageDialog(ex.Message, "Error loading file");
                    await md.ShowAsync();
                }
            }
        }

        private void OnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (m_renderer.IsSelecting)
            {
                m_renderer.StopSelectionMode();
                ToggleBrushButton(mSavedButton);
            }
            else
            {
                m_renderer.StartSelectionMode();
                mSavedButton = mCurrentBrushBtn;
                ToggleBrushButton((AppBarToggleButton)sender);
            }
        }
    }

}
