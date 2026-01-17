namespace BetterSettings.App.Models;

public sealed class SettingItem
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string[] Synonyms { get; init; } = Array.Empty<string>();
    public string Category { get; init; } = string.Empty;
    public LaunchType LaunchType { get; init; }
    public string LaunchTarget { get; init; } = string.Empty;
    public AvailabilityRule? Availability { get; init; }
    public bool IsAvailable { get; set; } = true;

    // New fields for improved UX
    public string Breadcrumbs { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string[] Tags { get; init; } = Array.Empty<string>();

    // Computed: breadcrumb segments for UI rendering
    public string[] BreadcrumbSegments => string.IsNullOrEmpty(Breadcrumbs)
        ? (string.IsNullOrEmpty(Category) ? Array.Empty<string>() : Category.Split(" / "))
        : Breadcrumbs.Split(" > ");
}
