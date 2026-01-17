namespace BetterSettings.App.Models;

public sealed class SearchResult
{
    public SettingItem Item { get; init; } = new();
    public double Score { get; init; }
    public MatchKind MatchKind { get; init; }
    public string MatchedOn { get; init; } = string.Empty;

    // The specific text that triggered the match (e.g., the synonym that matched)
    public string MatchedText { get; init; } = string.Empty;

    // UI helpers
    public bool IsAvailable => Item.IsAvailable;
    public double Opacity => IsAvailable ? 1.0 : 0.5;
    public string AvailabilityText => IsAvailable ? string.Empty : "(Unavailable)";

    // Match strength for visual cues (0.0 = weakest, 1.0 = exact match)
    public double MatchStrength => MatchKind switch
    {
        MatchKind.Exact => 1.0,
        MatchKind.SynonymExact => 0.95,
        MatchKind.Prefix => 0.85,
        MatchKind.Token => 0.7,
        MatchKind.Fuzzy => 0.5,
        MatchKind.Semantic => 0.6,
        _ => 0.5
    };

    // Visual indicator: should show accent bar (strong match)
    public bool IsStrongMatch => MatchKind is MatchKind.Exact or MatchKind.SynonymExact or MatchKind.Prefix;

    // Visual indicator: matched via synonym (show hint)
    public bool IsSynonymMatch => MatchedOn == "synonym" && !string.IsNullOrEmpty(MatchedText);

    // Display text for the match reason (only shown for synonym/semantic matches)
    public string MatchHint => IsSynonymMatch ? $"Matched: {MatchedText}" : string.Empty;

    // Helper for visibility: has description
    public bool HasDescription => !string.IsNullOrEmpty(Item.Description);

    // Helper for visibility: has breadcrumbs
    public bool HasBreadcrumbs => !string.IsNullOrEmpty(Item.Breadcrumbs);

    // Show description only when there's no match hint showing
    public bool ShowDescription => HasDescription && !IsSynonymMatch;
}

public enum MatchKind
{
    Exact,
    SynonymExact,
    Prefix,
    Token,
    Fuzzy,
    Semantic // New: for embedding-based matches
}
