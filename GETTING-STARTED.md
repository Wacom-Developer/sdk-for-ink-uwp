# Getting Started 

## Windows 10 UWP Development Environment

The WILL SDK for Ink requires Windows 10 with Visual Studio 2019 or above.
A sample application has been provided for the Universal Windows Platform (UWP).

## Download the SDK

Download the SDK from https://developer.wacom.com/developer-dashboard

* Login using your Wacom ID
* Select **Downloads for ink**
* Download **WILL SDK for ink for Windows 10 (UWP/WPF) (version 3.0)**
* Accept the End User License Agreement to use the SDK

The downloaded Zip file contains the SDK.


## SDK License

The SDK does not require a license key. Use of the SDK is subject to the End User License Agreement. 

## Using the WILL SDK

The SDK contains the NuGet packages required by the application code.
> Ultimately the packages will be available in the public NuGet.org Gallery provided for the .NET community.
> Until they are in place the same result can be achieved by using a local copy:

•	copy the SDK *NuGets* folder alongside the sample project code
•	follow the instructions below to add the NugGet packages to the project references
    

## Sample Application

Build the application in Visual Studio 2019.

- Open the demo application WILL3-DemoApp-UWP.sln solution file
- If prompted, select Developer Mode in Visual Studio
- To compile the application you will need to add the NuGet packages to the project. One way is as follows:
    - Copy the sdk folder containing NuGet packages to a local folder
      e.g. *WILL NuGets* 
    - Right-click the project in Solution Explorer
    - Select *Manage NuGet Packages...* 
      Wacom packages may be listed as unavailable. To resolve:
    - Select Settings (gear wheel) - displays the Options dialog
    - Select New (+)
    - This will set new source Name: to 'Package Source' by default.
      Change to *Wacom*
    - Click the browse button ...
    - Navigate to the *WILL NuGets* folder
    - Click Update
    - On return to NuGet Package Manager change Package Source: to the newly created source folder (e.g. *Wacom*)
      Wacom packages should now be shown as available (if necessary select Install):
        Wacom.Ink
        Wacom.Ink.Rendering.UWP
- Generate a certificate: 
    - Right-click the solution and go to Properties. Select Application and press the Package Manifest... button. 
    - From there, select the Packaging tab and Choose Certificate...
    - Create a certificate and press OK. 
- Build the project as Debug x86 or Debug x64
- Run the program to display the main window
- By selecting different options in the menu, the rendering options can be explored.
- Options are included to clear the display and to save, or read a WILL Universal ink file



![Demo app screenshot](media/will3-demo-uwp.png)

## API Reference

Having installed the NuGet packages the API can be viewed through IntelliSense or Object Browser in Visual Studio.

----
