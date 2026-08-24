using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AppLauncher.Models;

namespace AppLauncher.Services
{
    /// <summary>
    /// Extracts and caches real application icons in memory. Extraction runs on a background
    /// thread so scrolling/typing in the UI never blocks (spec §11-12: non-blocking icon loading).
    /// </summary>
    public class IconService
    {
        private readonly Dictionary<string, BitmapSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public Task<BitmapSource?> GetIconAsync(ApplicationItem item)
        {
            string key = item.ExecutablePath.ToLowerInvariant();

            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var cached)) return Task.FromResult(cached);
            }

            return Task.Run(() =>
            {
                BitmapSource? bitmap;
                try
                {
                    bitmap = ExtractIcon(item.ExecutablePath);
                    bitmap?.Freeze();
                }
                catch
                {
                    bitmap = null;
                }

                lock (_lock) { _cache[key] = bitmap; }
                return bitmap;
            });
        }

        private static BitmapSource? ExtractIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null; // web shortcut: no exe icon
            if (!File.Exists(path)) return null;

            using Icon? icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            using Bitmap bitmap = icon.ToBitmap();
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
