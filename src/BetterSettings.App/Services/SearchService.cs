using System.Text;
using BetterSettings.App.Models;

namespace BetterSettings.App;

public sealed class SearchService
{
    private readonly IReadOnlyList<IndexedItem> _index;
    private readonly Dictionary<string, List<int>> _tokenIndex;

    public SearchService(IndexStore indexStore)
    {
        _index = indexStore.Items.Select((item, idx) => new IndexedItem(item, idx)).ToList();
        _tokenIndex = BuildTokenIndex(_index);
    }

    public IReadOnlyList<SearchResult> Search(string query, int maxResults = 12)
    {
        var normalized = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return _index
                .OrderByDescending(i => i.Item.IsAvailable)
                .ThenBy(i => i.Item.DisplayName)
                .Take(maxResults)
                .Select(i => new SearchResult
                {
                    Item = i.Item,
                    Score = 0,
                    MatchKind = MatchKind.Token,
                    MatchedOn = "default"
                })
                .ToList();
        }

        var candidateIds = GetCandidates(normalized);
        var results = new List<SearchResult>();

        foreach (var id in candidateIds)
        {
            var indexed = _index[id];
            var scored = ScoreItem(indexed, normalized);
            if (scored.Score > 0)
            {
                results.Add(scored);
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Item.IsAvailable)
            .ThenBy(r => r.Item.DisplayName)
            .Take(maxResults)
            .ToList();
    }

    private SearchResult ScoreItem(IndexedItem indexed, string normalizedQuery)
    {
        var bestScore = 0.0;
        var matchKind = MatchKind.Token;
        var matchedOn = "display";

        // Exact match on display name
        if (indexed.DisplayName == normalizedQuery)
        {
            bestScore = 100;
            matchKind = MatchKind.Exact;
        }
        // Exact match on synonym
        else if (indexed.Synonyms.Contains(normalizedQuery))
        {
            bestScore = 95;
            matchKind = MatchKind.SynonymExact;
            matchedOn = "synonym";
        }
        // Prefix match on display name
        else if (indexed.DisplayName.StartsWith(normalizedQuery))
        {
            bestScore = 90;
            matchKind = MatchKind.Prefix;
        }
        // Prefix match on synonym
        else if (indexed.Synonyms.Any(s => s.StartsWith(normalizedQuery)))
        {
            bestScore = 85;
            matchKind = MatchKind.Prefix;
            matchedOn = "synonym";
        }
        // Contains match on display name
        else if (indexed.DisplayName.Contains(normalizedQuery))
        {
            bestScore = 75;
            matchKind = MatchKind.Token;
        }
        // Contains match on synonym
        else if (indexed.Synonyms.Any(s => s.Contains(normalizedQuery)))
        {
            bestScore = 70;
            matchKind = MatchKind.Token;
            matchedOn = "synonym";
        }
        // Token overlap
        else
        {
            var overlap = TokenOverlap(indexed.AllTokens, normalizedQuery);
            if (overlap > 0)
            {
                bestScore = 60 + overlap * 8;
                matchKind = MatchKind.Token;
            }
        }

        // Fuzzy matching for low scores
        if (bestScore < 70)
        {
            var fuzzyScore = FuzzyScore(normalizedQuery, indexed);
            if (fuzzyScore > bestScore)
            {
                bestScore = fuzzyScore;
                matchKind = MatchKind.Fuzzy;
            }
        }

        // Penalty for unavailable items
        if (!indexed.Item.IsAvailable)
        {
            bestScore -= 25;
        }

        // Bonus for category match
        if (indexed.Category.Contains(normalizedQuery))
        {
            bestScore += 4;
            matchedOn = "category";
        }

        return new SearchResult
        {
            Item = indexed.Item,
            Score = Math.Max(0, bestScore),
            MatchKind = matchKind,
            MatchedOn = matchedOn
        };
    }

    private static Dictionary<string, List<int>> BuildTokenIndex(IReadOnlyList<IndexedItem> items)
    {
        var dict = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Count; i++)
        {
            foreach (var token in items[i].AllTokens)
            {
                if (!dict.TryGetValue(token, out var list))
                {
                    list = new List<int>();
                    dict[token] = list;
                }

                if (!list.Contains(i))
                {
                    list.Add(i);
                }
            }
        }

        return dict;
    }

    private IEnumerable<int> GetCandidates(string normalizedQuery)
    {
        var tokens = Tokenize(normalizedQuery).ToArray();
        if (tokens.Length == 0)
        {
            return Enumerable.Range(0, _index.Count);
        }

        var candidates = new HashSet<int>();
        foreach (var token in tokens)
        {
            if (_tokenIndex.TryGetValue(token, out var ids))
            {
                foreach (var id in ids)
                {
                    candidates.Add(id);
                }
            }
        }

        // Also check for partial token matches
        foreach (var token in tokens)
        {
            foreach (var kvp in _tokenIndex)
            {
                if (kvp.Key.StartsWith(token) || kvp.Key.Contains(token))
                {
                    foreach (var id in kvp.Value)
                    {
                        candidates.Add(id);
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            return Enumerable.Range(0, _index.Count);
        }

        return candidates;
    }

    private static int TokenOverlap(IReadOnlyCollection<string> tokens, string normalizedQuery)
    {
        var queryTokens = Tokenize(normalizedQuery).ToArray();
        if (queryTokens.Length == 0)
        {
            return 0;
        }

        var overlap = 0;
        foreach (var token in queryTokens)
        {
            if (tokens.Contains(token) || tokens.Any(t => t.StartsWith(token)))
            {
                overlap++;
            }
        }

        return overlap;
    }

    private static double FuzzyScore(string query, IndexedItem indexed)
    {
        var candidates = new List<string> { indexed.DisplayName };
        candidates.AddRange(indexed.Synonyms);

        var best = 0.0;
        foreach (var candidate in candidates)
        {
            var distance = DamerauLevenshteinDistance(query, candidate);
            var maxLen = Math.Max(query.Length, candidate.Length);
            if (maxLen == 0)
            {
                continue;
            }

            var similarity = 1.0 - (double)distance / maxLen;
            if (similarity > best)
            {
                best = similarity;
            }
        }

        if (best < 0.6)
        {
            return 0;
        }

        return 50 + best * 20;
    }

    private static int DamerauLevenshteinDistance(string source, string target)
    {
        var m = source.Length;
        var n = target.Length;
        var dp = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
        {
            dp[i, 0] = i;
        }

        for (var j = 0; j <= n; j++)
        {
            dp[0, j] = j;
        }

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                var deletion = dp[i - 1, j] + 1;
                var insertion = dp[i, j - 1] + 1;
                var substitution = dp[i - 1, j - 1] + cost;

                var value = Math.Min(Math.Min(deletion, insertion), substitution);

                if (i > 1 && j > 1 && source[i - 1] == target[j - 2] && source[i - 2] == target[j - 1])
                {
                    value = Math.Min(value, dp[i - 2, j - 2] + cost);
                }

                dp[i, j] = value;
            }
        }

        return dp[m, n];
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                sb.Append(ch);
            }
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<string> Tokenize(string normalizedInput)
    {
        return normalizedInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class IndexedItem
    {
        public IndexedItem(SettingItem item, int id)
        {
            Item = item;
            Id = id;
            DisplayName = Normalize(item.DisplayName);
            Synonyms = item.Synonyms.Select(Normalize).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            Category = Normalize(item.Category);
            var tokens = new HashSet<string>(Tokenize(DisplayName));
            foreach (var syn in Synonyms)
            {
                foreach (var token in Tokenize(syn))
                {
                    tokens.Add(token);
                }
            }
            foreach (var token in Tokenize(Category))
            {
                tokens.Add(token);
            }

            AllTokens = tokens;
        }

        public int Id { get; }
        public SettingItem Item { get; }
        public string DisplayName { get; }
        public List<string> Synonyms { get; }
        public string Category { get; }
        public HashSet<string> AllTokens { get; }
    }
}
