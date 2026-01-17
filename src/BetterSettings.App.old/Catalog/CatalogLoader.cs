using System.Text.Json;
using System.Text.Json.Serialization;
using BetterSettings.Models;

namespace BetterSettings;

public sealed class CatalogLoader
{
    private readonly JsonSerializerOptions _jsonOptions = new()
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
        var catalog = JsonSerializer.Deserialize<CatalogRoot>(json, _jsonOptions);
        return catalog?.Items ?? new List<SettingItem>();
    }

    private sealed class CatalogRoot
    {
        public int Version { get; set; }
        public List<SettingItem> Items { get; set; } = new();
    }
}
