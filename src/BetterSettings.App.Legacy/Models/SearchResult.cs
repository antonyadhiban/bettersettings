namespace BetterSettings.Models;

public sealed class SearchResult
{
    public SettingItem Item { get; init; } = new SettingItem();
    public double Score { get; init; }
    public MatchKind MatchKind { get; init; }
    public string MatchedOn { get; init; } = string.Empty;

    public bool IsAvailable => Item.IsAvailable;
    public double Opacity => IsAvailable ? 1.0 : 0.4;
    public string AvailabilityText => IsAvailable ? string.Empty : "(Unavailable)";
}

public enum MatchKind
{
    Exact,
    SynonymExact,
    Prefix,
    Token,
    Fuzzy
}
