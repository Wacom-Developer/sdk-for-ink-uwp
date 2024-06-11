using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Wacom.Ink.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WacomInkDemoUWP
{
    abstract class PngDataProvider
    {
        public abstract Task<byte[]> GetPngBytesAsync();

        public abstract Task<PixelData> GetPixelDataAsync();

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

	class PngDataFromAppResourceProvider : PngDataProvider
    {
        public Uri AppResourceUri { get; }

        public PngDataFromAppResourceProvider(string appResourceUri)
        {
            AppResourceUri = new Uri(appResourceUri);
        }

        public override async Task<byte[]> GetPngBytesAsync()
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(AppResourceUri);

            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
            {
                DataReader reader = new DataReader(fileStream.GetInputStreamAt(0));
                byte[] bytes = new byte[fileStream.Size];
                await reader.LoadAsync((uint)fileStream.Size);
                reader.ReadBytes(bytes);
                return bytes;
            }
        }

        public override async Task<PixelData> GetPixelDataAsync()
        {
            return await GetPixelDataAsync(AppResourceUri);
        }
    }

    class PngDataFromByteArrayProvider : PngDataProvider
    {
        public byte[] PngData { get; }

        public PngDataFromByteArrayProvider(byte[] pngData)
        {
            PngData = pngData;
        }

        public override Task<byte[]> GetPngBytesAsync()
        {
            return Task.FromResult(PngData);
        }

        public override async Task<PixelData> GetPixelDataAsync()
        {
            return await GetPixelDataAsync(PngData);
        }
    }
}
