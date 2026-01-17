using System.Text.Json;
using System.Text.Json.Serialization;
using BetterSettings.App.Models;

namespace BetterSettings.App;

public sealed class CatalogLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<SettingItem> LoadEmbeddedCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Catalog", "embedded-catalog.json");
        if (!File.Exists(path))
        {
            return Array.Empty<SettingItem>();
        }

        var json = File.ReadAllText(path);
        var catalog = JsonSerializer.Deserialize<CatalogRoot>(json, JsonOptions);
        return catalog?.Items?.AsReadOnly() ?? (IReadOnlyList<SettingItem>)Array.Empty<SettingItem>();
    }

    private sealed class CatalogRoot
    {
        public int Version { get; set; }
        public List<SettingItem> Items { get; set; } = new();
    }
}
