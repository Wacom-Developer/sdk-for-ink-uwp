using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Tutorial_01
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
		}
		private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
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
			else
			{
				// Remove the UI from the title bar if in-app back stack is empty.
				SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
					AppViewBackButtonVisibility.Collapsed;
			}
		}
		protected override void OnNavigatedFrom(NavigationEventArgs e)
		{
			Windows.UI.Core.SystemNavigationManager.GetForCurrentView().BackRequested -= MainPage_BackRequested;
		}
		private void ButtonPart1_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part1));
		}

		private void ButtonPart2_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part2));
		}

		private void ButtonPart3_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part3));
		}

		private void ButtonPart4_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part4));
		}

		private void ButtonPart5_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part5));
		}

		private void ButtonPart6_Clicked(object sender, RoutedEventArgs e)
		{
			this.Frame.Navigate(typeof(Part6));
		}
	}
}
