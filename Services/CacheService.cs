using System.IO;
using System.Text.Json;
using AppLauncher.Models;

namespace AppLauncher.Services
{
    /// <summary>
    /// Persists the scanned application list to disk so the next startup can show results
    /// instantly while a fresh background rescan confirms/updates the list (spec §6).
    /// </summary>
    public class CacheService
    {
        private readonly string _cacheFilePath;

        public CacheService()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppLauncher");
            Directory.CreateDirectory(dir);
            _cacheFilePath = Path.Combine(dir, "cache.json");
        }

        public async Task<List<ApplicationItem>?> LoadAsync()
        {
            try
            {
                if (!File.Exists(_cacheFilePath)) return null;
                await using var stream = File.OpenRead(_cacheFilePath);
                return await JsonSerializer.DeserializeAsync<List<ApplicationItem>>(stream);
            }
            catch (Exception ex)
            {
                Helpers.Logger.LogError(ex, "CacheService.LoadAsync");
                return null;
            }
        }

        public async Task SaveAsync(List<ApplicationItem> apps)
        {
            try
            {
                await using var stream = File.Create(_cacheFilePath);
                await JsonSerializer.SerializeAsync(stream, apps);
            }
            catch (Exception ex)
            {
                Helpers.Logger.LogError(ex, "CacheService.SaveAsync");
                // Non-fatal: the cache is a performance optimization, not required for correctness
            }
        }
    }
}
