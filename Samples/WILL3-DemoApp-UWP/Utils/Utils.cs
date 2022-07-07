using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Wacom.Ink.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WacomInkDemoUWP
{
    public static class Utils
    {
        /// <summary>
        /// Disposes an object if it is disposable and not null
        /// </summary>
        /// <param name="obj">object to Dispose</param>
        public static void SafeDispose(object obj)
        {
            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Loads bitmap pixel data from app resources
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static async Task<PixelData> GetPixelDataAsync(Uri uri)
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

        public static async Task<PixelData> GetPixelDataAsync(byte[] fileBytes)
        {
            using (InMemoryRandomAccessStream fileStream = new InMemoryRandomAccessStream())
            {
                await fileStream.WriteAsync(fileBytes.AsBuffer());
                fileStream.Seek(0);

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
