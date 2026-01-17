using BetterSettings.App.Models;

namespace BetterSettings.App;

public sealed class IndexStore
{
    private readonly IReadOnlyList<SettingItem> _items;

    public IndexStore(
        CatalogLoader loader,
        PlatformService platformService,
        SettingsDiscoveryService? discoveryService = null,
        IndexCacheService? cacheService = null)
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "Catalog", "embedded-catalog.json");
        var catalogHash = IndexCacheService.ComputeCatalogHash(catalogPath);
        var windowsBuild = platformService.BuildNumber;

        // Try to load from cache first
        if (cacheService != null)
        {
            var cached = cacheService.TryLoadCache(catalogHash, windowsBuild);
            if (cached != null)
            {
                _items = cached;
                return;
            }
        }

        // Build fresh index
        var items = new List<SettingItem>();

        // Load from embedded catalog (primary source)
        var catalogItems = loader.LoadEmbeddedCatalog().ToList();
        foreach (var item in catalogItems)
        {
            item.IsAvailable = platformService.IsAvailable(item.Availability);
        }
        items.AddRange(catalogItems);

        // Discover additional items from registry (if service provided)
        if (discoveryService != null)
        {
            try
            {
                var discovered = discoveryService.DiscoverFromRegistry();
                var mergedItems = MergeAndDedupe(items, discovered);
                items = mergedItems;
            }
            catch
            {
                // Registry discovery is optional; continue with catalog only
            }
        }

        _items = items;

        // Save to cache for next startup
        cacheService?.SaveCache(_items, catalogHash, windowsBuild);
    }

    public IReadOnlyList<SettingItem> Items => _items;

    /// <summary>
    /// Merges discovered items with catalog items, deduplicating by launch target.
    /// Catalog items take precedence (they have curated metadata).
    /// </summary>
    private static List<SettingItem> MergeAndDedupe(List<SettingItem> catalog, IReadOnlyList<SettingItem> discovered)
    {
        // Index catalog items by launch target for deduplication
        var seenTargets = new HashSet<string>(
            catalog.Select(i => NormalizeLaunchTarget(i.LaunchTarget)),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<SettingItem>(catalog);

        foreach (var item in discovered)
        {
            var normalizedTarget = NormalizeLaunchTarget(item.LaunchTarget);

            // Skip if we already have this target from the catalog
            if (seenTargets.Contains(normalizedTarget))
                continue;

            seenTargets.Add(normalizedTarget);
            result.Add(item);
        }

        return result;
    }

    private static string NormalizeLaunchTarget(string target)
    {
        if (string.IsNullOrEmpty(target))
            return string.Empty;

        // Normalize ms-settings: URIs
        return target.ToLowerInvariant().Trim();
    }
}
