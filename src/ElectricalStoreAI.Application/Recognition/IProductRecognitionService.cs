namespace ElectricalStoreAI.Application.Recognition;

public interface IProductRecognitionService
{
    Task<RecognitionResult> FindCandidatesAsync(float[] queryEmbedding, CancellationToken cancellationToken = default);
}
