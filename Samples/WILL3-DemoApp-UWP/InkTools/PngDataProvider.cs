using System;
using System.Threading.Tasks;
using Wacom.Ink.Rendering;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WacomInkDemoUWP
{
    public abstract class PngDataProvider
    {
        public abstract Task<byte[]> GetPngBytesAsync();

        public abstract Task<PixelData> GetPixelDataAsync();
    }

    public class PngDataFromAppResourceProvider : PngDataProvider
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
            return await Utils.GetPixelDataAsync(AppResourceUri);
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
            return await Utils.GetPixelDataAsync(PngData);
        }
    }
}
