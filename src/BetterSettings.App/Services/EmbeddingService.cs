using System.Numerics;
using BetterSettings.App.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace BetterSettings.App;

/// <summary>
/// Provides semantic search using embeddings.
/// Uses ONNX models when available, with word-vector fallback.
/// </summary>
public sealed class EmbeddingService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly Dictionary<string, float[]> _destinationEmbeddings = new();
    private readonly Dictionary<string, int> _wordIndex = new();
    private readonly List<SettingItem> _items;
    private readonly bool _useOnnx;

    // Simple word vectors for fallback (dimension = vocabulary size, capped)
    private const int MaxVocabSize = 1000;
    private const int EmbeddingDim = 384; // MiniLM-L6 dimension

    public EmbeddingService(IReadOnlyList<SettingItem> items, string? modelPath = null)
    {
        _items = items.ToList();

        // Try to load ONNX model if provided
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            try
            {
                var options = new SessionOptions();
                options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                _session = new InferenceSession(modelPath, options);
                _useOnnx = true;
            }
            catch
            {
                _useOnnx = false;
            }
        }
        else
        {
            _useOnnx = false;
        }

        // Build word index and compute embeddings
        BuildVocabulary();
        PrecomputeEmbeddings();
    }

    /// <summary>
    /// Finds semantically similar destinations for a query.
    /// </summary>
    public IReadOnlyList<(SettingItem Item, double Similarity)> FindSimilar(string query, int topK = 5)
    {
        var queryEmbedding = ComputeEmbedding(query);
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            return Array.Empty<(SettingItem, double)>();

        var results = new List<(SettingItem Item, double Similarity)>();

        foreach (var item in _items)
        {
            if (!_destinationEmbeddings.TryGetValue(item.Id, out var itemEmbedding))
                continue;

            var similarity = CosineSimilarity(queryEmbedding, itemEmbedding);
            if (similarity > 0.1) // Minimum threshold
            {
                results.Add((item, similarity));
            }
        }

        return results
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();
    }

    private void BuildVocabulary()
    {
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in _items)
        {
            var text = GetSearchableText(item);
            var words = Tokenize(text);

            foreach (var word in words)
            {
                if (!wordCounts.TryGetValue(word, out var count))
                    count = 0;
                wordCounts[word] = count + 1;
            }
        }

        // Build vocabulary from most common words
        var sortedWords = wordCounts
            .OrderByDescending(kv => kv.Value)
            .Take(MaxVocabSize)
            .Select((kv, idx) => (Word: kv.Key, Index: idx));

        foreach (var (word, index) in sortedWords)
        {
            _wordIndex[word] = index;
        }
    }

    private void PrecomputeEmbeddings()
    {
        foreach (var item in _items)
        {
            var text = GetSearchableText(item);
            var embedding = ComputeEmbedding(text);
            if (embedding != null)
            {
                _destinationEmbeddings[item.Id] = embedding;
            }
        }
    }

    private float[]? ComputeEmbedding(string text)
    {
        if (_useOnnx && _session != null)
        {
            return ComputeOnnxEmbedding(text);
        }
        else
        {
            return ComputeWordVectorEmbedding(text);
        }
    }

    private float[]? ComputeOnnxEmbedding(string text)
    {
        if (_session == null) return null;

        try
        {
            // Simple tokenization for ONNX models
            // Note: For production use, a proper tokenizer (like BertTokenizer) should be used
            var tokens = SimpleWordPieceTokenize(text);
            var seqLength = tokens.Length;

            // Create flat arrays for tensors
            var inputIdsFlat = new long[seqLength];
            var attentionMaskFlat = new long[seqLength];

            for (int i = 0; i < seqLength; i++)
            {
                inputIdsFlat[i] = tokens[i];
                attentionMaskFlat[i] = 1;
            }

            var inputIdsTensor = new DenseTensor<long>(inputIdsFlat, new[] { 1, seqLength });
            var attentionMaskTensor = new DenseTensor<long>(attentionMaskFlat, new[] { 1, seqLength });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Mean pooling over sequence dimension
            var embeddingDim = output.Dimensions[2];
            var embedding = new float[embeddingDim];
            var outputSeqLen = output.Dimensions[1];

            for (int d = 0; d < embeddingDim; d++)
            {
                float sum = 0;
                for (int s = 0; s < outputSeqLen; s++)
                {
                    sum += output[0, s, d];
                }
                embedding[d] = sum / outputSeqLen;
            }

            // L2 normalize
            Normalize(embedding);
            return embedding;
        }
        catch
        {
            return ComputeWordVectorEmbedding(text);
        }
    }

    private float[] ComputeWordVectorEmbedding(string text)
    {
        // Simple TF-based bag-of-words embedding
        var words = Tokenize(text);
        var embedding = new float[Math.Min(_wordIndex.Count, MaxVocabSize)];

        if (embedding.Length == 0)
            return Array.Empty<float>();

        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words)
        {
            if (!wordCounts.TryGetValue(word, out var count))
                count = 0;
            wordCounts[word] = count + 1;
        }

        foreach (var (word, count) in wordCounts)
        {
            if (_wordIndex.TryGetValue(word, out var idx) && idx < embedding.Length)
            {
                // TF-IDF style weighting
                embedding[idx] = (float)(1 + Math.Log(count));
            }
        }

        Normalize(embedding);
        return embedding;
    }

    private static long[] SimpleWordPieceTokenize(string text)
    {
        // Very simple tokenization - just convert words to hash-based IDs
        // For production, use a proper tokenizer
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '-', '_', '/', '\\', '(', ')', '[', ']' }, 
                   StringSplitOptions.RemoveEmptyEntries);

        var tokens = new List<long> { 101 }; // [CLS]
        foreach (var word in words.Take(62)) // Leave room for [SEP]
        {
            // Simple hash to vocab ID (assuming 30000 vocab size)
            tokens.Add(Math.Abs(word.GetHashCode()) % 30000 + 1000);
        }
        tokens.Add(102); // [SEP]

        return tokens.ToArray();
    }

    private static string[] Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '-', '_', '/', '\\', '(', ')', '[', ']', '>', '<', '&' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Distinct()
            .ToArray();
    }

    private static string GetSearchableText(SettingItem item)
    {
        var parts = new List<string>
        {
            item.DisplayName,
            item.Category,
            item.Breadcrumbs,
            item.Description
        };

        parts.AddRange(item.Synonyms);
        parts.AddRange(item.Tags);

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0;

        double dot = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        // Vectors are already normalized
        return dot;
    }

    private static void Normalize(float[] vector)
    {
        double sum = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            sum += vector[i] * vector[i];
        }

        if (sum <= 0) return;

        var norm = (float)Math.Sqrt(sum);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
