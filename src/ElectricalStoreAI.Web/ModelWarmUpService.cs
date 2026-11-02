using ElectricalStoreAI.Application.Recognition;

namespace ElectricalStoreAI.Web;

/// <summary>
/// Loads the ONNX model in the background right after startup so the first scan does not pay the
/// multi-second model load. Failures are logged; the UI reports model status separately.
/// </summary>
public sealed class ModelWarmUpService : BackgroundService
{
    private readonly IImageEmbeddingService _embeddingService;
    private readonly ILogger<ModelWarmUpService> _logger;

    public ModelWarmUpService(IImageEmbeddingService embeddingService, ILogger<ModelWarmUpService> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        try
        {
            await _embeddingService.WarmUpAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding model warm-up failed; scanning will report a setup error until fixed.");
        }
    }
}
