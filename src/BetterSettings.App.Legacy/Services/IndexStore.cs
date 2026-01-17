using BetterSettings.Models;

namespace BetterSettings;

public sealed class IndexStore
{
    private readonly IReadOnlyList<SettingItem> _items;

    public IndexStore(CatalogLoader loader, PlatformService platformService)
    {
        var items = loader.LoadEmbeddedCatalog().ToList();
        foreach (var item in items)
        {
            item.IsAvailable = platformService.IsAvailable(item.Availability);
        }

        _items = items;
    }

    public IReadOnlyList<SettingItem> Items => _items;
}
