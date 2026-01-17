namespace BetterSettings.Models;

public sealed class AvailabilityRule
{
    public int? MinBuild { get; init; }
    public string[]? RequiredTags { get; init; }
}
