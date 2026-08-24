using System.Diagnostics;
using System.IO;
using AppLauncher.Models;

namespace AppLauncher.Services
{
    /// <summary>
    /// Launches a resolved ApplicationItem. Only launches paths that are either a real file
    /// that exists on disk or a well-formed http(s) URL - never an arbitrary/forged path (spec §22).
    /// </summary>
    public class ApplicationLauncher
    {
        public bool Launch(ApplicationItem item)
        {
            if (!IsPathSafe(item.ExecutablePath)) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = item.ExecutablePath,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                };

                if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && Directory.Exists(item.WorkingDirectory))
                {
                    psi.WorkingDirectory = item.WorkingDirectory;
                }

                Process.Start(psi);

                item.LaunchCount++;
                item.LastLaunchedUtc = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                Helpers.Logger.LogError(ex, $"Launch failed for '{item.ExecutablePath}'");
                return false;
            }
        }

        private static bool IsPathSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate(path, UriKind.Absolute, out _);
            }

            return File.Exists(path);
        }
    }
}
