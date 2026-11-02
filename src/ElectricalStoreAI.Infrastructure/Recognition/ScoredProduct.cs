using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Products;

namespace ElectricalStoreAI.Infrastructure.Recognition;

public sealed record ScoredProduct(Product Product, float[] Embedding, double SimilarityScore);
