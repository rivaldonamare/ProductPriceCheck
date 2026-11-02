using System.Collections.Concurrent;
using System.Diagnostics;
using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Similarity;
using Microsoft.Extensions.Options;

namespace ElectricalStoreAI.Infrastructure.Recognition;

/// <summary>
/// Process-wide cache of embedding vectors keyed by embedding id. Vectors are immutable per id (re-indexing
/// creates new ids), so entries never go stale; deleted ids are simply never requested again.
/// </summary>
public sealed class EmbeddingVectorCache
{
    private readonly ConcurrentDictionary<Guid, float[]> _vectors = new();

    public bool TryGet(Guid embeddingId, out float[] vector) => _vectors.TryGetValue(embeddingId, out vector!);

    public void Set(Guid embeddingId, float[] vector) => _vectors[embeddingId] = vector;
}

public sealed class ProductRecognitionService : IProductRecognitionService
{
    private readonly IProductRepository _productRepository;
    private readonly IImageEmbeddingService _embeddingService;
    private readonly EmbeddingVectorCache _cache;
    private readonly RecognitionOptions _options;

    public ProductRecognitionService(
        IProductRepository productRepository,
        IImageEmbeddingService embeddingService,
        EmbeddingVectorCache cache,
        IOptions<RecognitionOptions> options)
    {
        _productRepository = productRepository;
        _embeddingService = embeddingService;
        _cache = cache;
        _options = options.Value;
    }

    public async Task<RecognitionResult> FindCandidatesAsync(float[] queryEmbedding, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Product rows (and therefore prices / active flags) are always read fresh from the database;
        // only the immutable vectors are cached. Only embeddings of the current model key are compared.
        var refs = await _productRepository.GetActiveEmbeddingRefsAsync(_embeddingService.ModelKey, cancellationToken);

        var missing = refs.Select(entry => entry.EmbeddingId).Where(id => !_cache.TryGet(id, out _)).ToList();
        if (missing.Count > 0)
        {
            var loaded = await _productRepository.GetEmbeddingVectorsAsync(missing, cancellationToken);
            foreach (var (id, vector) in loaded)
            {
                _cache.Set(id, vector);
            }
        }

        var scored = new List<ScoredProduct>(refs.Count);
        foreach (var entry in refs)
        {
            if (_cache.TryGet(entry.EmbeddingId, out var vector))
            {
                scored.Add(new ScoredProduct(entry.Product, vector, EmbeddingSimilarity.CosineSimilarity(queryEmbedding, vector)));
            }
        }

        var ranked = ProductCandidateAggregator.Rank(scored, _options.TopN);
        return new RecognitionResult(ranked, RecognitionConfidence.IsConfident(ranked, _options), refs.Count, stopwatch.Elapsed);
    }
}
