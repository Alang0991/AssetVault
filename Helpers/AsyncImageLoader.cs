using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AssetVault.Helpers
{
    public static class AsyncImageLoader
    {
        private static readonly ConcurrentDictionary<string, BitmapImage?> Cache = new();

        public static readonly DependencyProperty SourcePathProperty =
            DependencyProperty.RegisterAttached(
                "SourcePath",
                typeof(string),
                typeof(AsyncImageLoader),
                new PropertyMetadata(null, OnSourcePathChanged));

        public static void SetSourcePath(DependencyObject element, string? value)
        {
            element.SetValue(SourcePathProperty, value);
        }

        public static string? GetSourcePath(DependencyObject element)
        {
            return (string?)element.GetValue(SourcePathProperty);
        }

        private static async void OnSourcePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
                return;

            string? path = e.NewValue as string;

            image.Source = null;

            if (string.IsNullOrWhiteSpace(path))
                return;

            var requestedPath = path;

            BitmapImage? bitmap = await LoadBitmapAsync(requestedPath);

            if (!string.Equals(GetSourcePath(image), requestedPath, StringComparison.OrdinalIgnoreCase))
                return;

            image.Source = bitmap;
        }

        private static Task<BitmapImage?> LoadBitmapAsync(string path)
        {
            if (Cache.TryGetValue(path, out var cached))
                return Task.FromResult(cached);

            return Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        Cache[path] = null;
                        return (BitmapImage?)null;
                    }

                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.DecodePixelWidth = 320;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();

                    Cache[path] = image;
                    return (BitmapImage?)image;
                }
                catch
                {
                    Cache[path] = null;
                    return (BitmapImage?)null;
                }
            });
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static void RemoveFromCache(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
                Cache.TryRemove(path, out _);
        }
    }
}