using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Wacom
{
    public static class Utils
    {
        /// <summary>
        /// Disposes an object if it is disposable and not null
        /// </summary>
        /// <param name="obj">object to Dispose</param>
        public static void SafeDispose(object obj)
        {
            IDisposable disposable = obj as IDisposable;

            if (disposable != null)
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

        /// <summary>
        /// Loads image file data from app resources
        /// </summary>
        public async static Task<byte[]> GetImageFileData(Uri uri)
        {
            var fileToRead = await StorageFile.GetFileFromApplicationUriAsync(uri);


            using (BinaryReader fileReader = new BinaryReader(await fileToRead.OpenStreamForReadAsync()))
            {
                byte[] data = new byte[fileReader.BaseStream.Length];
                fileReader.Read(data, 0, data.Length);
                return data;
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
