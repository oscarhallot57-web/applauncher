using System.Windows.Media.Imaging;

namespace AppLauncher.Models
{
    public enum AppSource
    {
        StartMenu,
        UrlShortcut
    }

    /// <summary>
    /// Represents a single launchable application discovered on the system.
    /// </summary>
    public class ApplicationItem
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Path of the .lnk / .url file this entry was discovered from (used for cache invalidation).</summary>
        public string SourceFilePath { get; set; } = string.Empty;

        public AppSource Source { get; set; } = AppSource.StartMenu;
        public string Category { get; set; } = "Application";
        public DateTime SourceLastWriteTimeUtc { get; set; }

        public int LaunchCount { get; set; }
        public DateTime LastLaunchedUtc { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public BitmapSource? Icon { get; set; }
    }
}
