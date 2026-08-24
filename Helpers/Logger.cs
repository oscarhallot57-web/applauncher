using System.IO;

namespace AppLauncher.Helpers
{
    /// <summary>Simple crash-safe file logger (spec §21: the app must never crash on bad input).</summary>
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object Lock = new();

        static Logger()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppLauncher");
            Directory.CreateDirectory(dir);
            LogFilePath = Path.Combine(dir, "log.txt");
        }

        public static void LogError(Exception? ex, string? context = null)
        {
            if (ex == null) return;
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(LogFilePath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context ?? string.Empty}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never itself throw
            }
        }
    }
}
