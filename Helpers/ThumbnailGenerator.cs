using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace AssetVault.Helpers
{
    public static class ThumbnailGenerator
    {
        public static string Generate(string sourceFile, string thumbnailFolder)
        {
            try
            {
                if (!File.Exists(sourceFile))
                    return "";

                Directory.CreateDirectory(thumbnailFolder);

                string thumbnailPath = Path.Combine(
                    thumbnailFolder,
                    Guid.NewGuid().ToString() + ".png");

                BitmapImage image = new BitmapImage();

                image.BeginInit();
                image.UriSource = new Uri(sourceFile);
                image.DecodePixelWidth = 256;
                image.EndInit();

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));

                using FileStream stream = new FileStream(thumbnailPath, FileMode.Create);

                encoder.Save(stream);

                return thumbnailPath;
            }
            catch
            {
                return "";
            }
        }
    }
}