using System.IO;
using AppLauncher.Models;

namespace AppLauncher.Services
{
    /// <summary>
    /// Discovers launchable applications from the Windows Start Menu (user + all-users),
    /// resolving .lnk shortcuts and .url internet shortcuts. Never throws: broken or
    /// inaccessible entries are skipped so one bad shortcut can never crash the scan.
    /// </summary>
    public class ApplicationScanner
    {
        private static readonly string[] NoiseKeywords =
        {
            "uninstall", "readme", "license", "changelog", "documentation", "help", "release notes"
        };

        public Task<List<ApplicationItem>> ScanAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var results = new Dictionary<string, ApplicationItem>(StringComparer.OrdinalIgnoreCase);

                var folders = new List<string>();
                string userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
                string commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

                if (Directory.Exists(userStartMenu)) folders.Add(userStartMenu);
                if (Directory.Exists(commonStartMenu)) folders.Add(commonStartMenu);

                foreach (var folder in folders)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ScanFolder(folder, results, cancellationToken);
                }

                return results.Values.ToList();
            }, cancellationToken);
        }

        private void ScanFolder(string folder, Dictionary<string, ApplicationItem> results, CancellationToken cancellationToken)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                             || f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return; // Folder inaccessible (permissions, etc.) - skip silently, keep scanning other folders
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ApplicationItem? item;
                try
                {
                    item = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
                        ? ResolveLnk(file)
                        : ResolveUrl(file);
                }
                catch
                {
                    continue; // Broken shortcut / unreadable file - ignore and continue (spec §21)
                }

                if (item == null || IsNoise(item.Name)) continue;

                string key = item.Name.ToLowerInvariant() + "|" + item.ExecutablePath.ToLowerInvariant();
                results.TryAdd(key, item); // user-level Start Menu is scanned first, so it wins on duplicates
            }
        }

        private ApplicationItem? ResolveLnk(string lnkPath)
        {
            if (!ShellLinkResolver.TryResolve(lnkPath, out string target, out string args, out string workDir))
                return null;

            if (string.IsNullOrWhiteSpace(target)) return null;

            var fileInfo = new FileInfo(lnkPath);

            return new ApplicationItem
            {
                Name = Path.GetFileNameWithoutExtension(lnkPath),
                ExecutablePath = target,
                Arguments = args,
                WorkingDirectory = string.IsNullOrWhiteSpace(workDir)
                    ? (Path.GetDirectoryName(target) ?? string.Empty)
                    : workDir,
                SourceFilePath = lnkPath,
                Source = AppSource.StartMenu,
                Category = "Application",
                SourceLastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            };
        }

        private ApplicationItem? ResolveUrl(string urlPath)
        {
            string? url = null;
            foreach (var line in File.ReadLines(urlPath))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    url = line[4..].Trim();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return null;

            var fileInfo = new FileInfo(urlPath);
            return new ApplicationItem
            {
                Name = Path.GetFileNameWithoutExtension(urlPath),
                ExecutablePath = url,
                SourceFilePath = urlPath,
                Source = AppSource.UrlShortcut,
                Category = "Lien web",
                SourceLastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            };
        }

        private static bool IsNoise(string name)
        {
            string lower = name.ToLowerInvariant();
            return NoiseKeywords.Any(lower.Contains);
        }
    }
}
