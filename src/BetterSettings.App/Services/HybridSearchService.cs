using BetterSettings.App.Models;

namespace BetterSettings.App;

/// <summary>
/// Combines lexical and semantic search with intelligent ranking.
/// Uses semantic search as a fallback when lexical matches are weak.
/// </summary>
public sealed class HybridSearchService : IDisposable
{
    private readonly SearchService _lexicalSearch;
    private readonly EmbeddingService? _embeddingService;

    // Thresholds for blending
    private const double LexicalStrongThreshold = 70.0;
    private const double SemanticMinThreshold = 0.2;
    private const double SemanticBoostFactor = 50.0;

    public HybridSearchService(
        IndexStore indexStore,
        EmbeddingService? embeddingService = null)
    {
        _lexicalSearch = new SearchService(indexStore);
        _embeddingService = embeddingService;
    }

    public IReadOnlyList<SearchResult> Search(string query, int maxResults = 12)
    {
        // Get lexical results first
        var lexicalResults = _lexicalSearch.Search(query, maxResults);

        // Check if lexical results are strong enough
        var hasStrongLexicalMatches = lexicalResults.Count > 0 &&
            lexicalResults[0].Score >= LexicalStrongThreshold;

        // If lexical is strong, return as-is
        if (hasStrongLexicalMatches || _embeddingService == null || string.IsNullOrWhiteSpace(query))
        {
            return lexicalResults;
        }

        // Get semantic results
        var semanticResults = _embeddingService.FindSimilar(query, maxResults);

        // Blend results
        return BlendResults(lexicalResults, semanticResults, maxResults);
    }

    private IReadOnlyList<SearchResult> BlendResults(
        IReadOnlyList<SearchResult> lexical,
        IReadOnlyList<(SettingItem Item, double Similarity)> semantic,
        int maxResults)
    {
        var resultMap = new Dictionary<string, BlendedResult>();

        // Add lexical results
        foreach (var result in lexical)
        {
            resultMap[result.Item.Id] = new BlendedResult
            {
                Item = result.Item,
                LexicalScore = result.Score,
                SemanticScore = 0,
                MatchKind = result.MatchKind,
                MatchedOn = result.MatchedOn,
                MatchedText = result.MatchedText
            };
        }

        // Merge semantic results
        foreach (var (item, similarity) in semantic)
        {
            if (similarity < SemanticMinThreshold)
                continue;

            if (resultMap.TryGetValue(item.Id, out var existing))
            {
                // Boost existing lexical result with semantic score
                existing.SemanticScore = similarity * SemanticBoostFactor;
            }
            else
            {
                // Add new semantic-only result
                resultMap[item.Id] = new BlendedResult
                {
                    Item = item,
                    LexicalScore = 0,
                    SemanticScore = similarity * SemanticBoostFactor,
                    MatchKind = MatchKind.Semantic,
                    MatchedOn = "semantic",
                    MatchedText = string.Empty
                };
            }
        }

        // Compute final scores and create results
        var results = resultMap.Values
            .Select(b => new SearchResult
            {
                Item = b.Item,
                Score = ComputeBlendedScore(b.LexicalScore, b.SemanticScore),
                MatchKind = b.LexicalScore > 0 ? b.MatchKind : MatchKind.Semantic,
                MatchedOn = b.LexicalScore > 0 ? b.MatchedOn : "semantic",
                MatchedText = b.MatchedText
            })
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Item.IsAvailable)
            .ThenBy(r => r.Item.DisplayName)
            .Take(maxResults)
            .ToList();

        return results;
    }

    private static double ComputeBlendedScore(double lexical, double semantic)
    {
        // If both scores are present, use weighted combination
        if (lexical > 0 && semantic > 0)
        {
            // Lexical takes precedence, semantic adds a boost
            return lexical + (semantic * 0.3);
        }

        // Otherwise, use whichever is available
        return Math.Max(lexical, semantic);
    }

    public void Dispose()
    {
        _embeddingService?.Dispose();
    }

    private sealed class BlendedResult
    {
        public SettingItem Item { get; set; } = new();
        public double LexicalScore { get; set; }
        public double SemanticScore { get; set; }
        public MatchKind MatchKind { get; set; }
        public string MatchedOn { get; set; } = string.Empty;
        public string MatchedText { get; set; } = string.Empty;
    }
}
