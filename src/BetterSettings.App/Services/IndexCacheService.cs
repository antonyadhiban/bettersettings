using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BetterSettings.App.Models;

namespace BetterSettings.App;

/// <summary>
/// Caches the computed index to avoid rebuilding on every startup.
/// Cache is invalidated when app version, Windows build, or catalog changes.
/// </summary>
public sealed class IndexCacheService
{
    private const string CacheFileName = "index-cache.json";
    private const int CacheVersion = 1;

    private readonly string _cacheDirectory;

    public IndexCacheService()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BetterSettings");
    }

    /// <summary>
    /// Tries to load the cached index if valid.
    /// </summary>
    public IReadOnlyList<SettingItem>? TryLoadCache(string catalogHash, int windowsBuild)
    {
        try
        {
            var cacheFile = Path.Combine(_cacheDirectory, CacheFileName);
            if (!File.Exists(cacheFile))
                return null;

            var json = File.ReadAllText(cacheFile);
            var cache = JsonSerializer.Deserialize<IndexCache>(json);

            if (cache == null)
                return null;

            // Validate cache key
            if (cache.Version != CacheVersion ||
                cache.CatalogHash != catalogHash ||
                cache.WindowsBuild != windowsBuild ||
                cache.AppVersion != GetAppVersion())
            {
                return null;
            }

            return cache.Items;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the index to cache.
    /// </summary>
    public void SaveCache(IReadOnlyList<SettingItem> items, string catalogHash, int windowsBuild)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            var cache = new IndexCache
            {
                Version = CacheVersion,
                CatalogHash = catalogHash,
                WindowsBuild = windowsBuild,
                AppVersion = GetAppVersion(),
                Items = items.ToList()
            };

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var cacheFile = Path.Combine(_cacheDirectory, CacheFileName);
            File.WriteAllText(cacheFile, json);
        }
        catch
        {
            // Caching is best-effort
        }
    }

    /// <summary>
    /// Computes a hash of the catalog file contents.
    /// </summary>
    public static string ComputeCatalogHash(string catalogPath)
    {
        try
        {
            if (!File.Exists(catalogPath))
                return string.Empty;

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(catalogPath);
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).Substring(0, 16);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetAppVersion()
    {
        return typeof(IndexCacheService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private sealed class IndexCache
    {
        public int Version { get; set; }
        public string CatalogHash { get; set; } = string.Empty;
        public int WindowsBuild { get; set; }
        public string AppVersion { get; set; } = string.Empty;
        public List<SettingItem> Items { get; set; } = new();
    }
}
