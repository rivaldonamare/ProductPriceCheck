namespace ElectricalStoreAI.Infrastructure.Recognition;

/// <summary>
/// Local image embedding model configuration (section "Recognition").
/// </summary>
public sealed class ImageEmbeddingOptions
{
    public const string DefaultModelKey = "clip-vit-b32";

    public const string DefaultModelPath = "Models/clip-vit-b32.onnx";

    /// <summary>Path as configured (shown to users in setup errors; never an absolute path).</summary>
    public string ConfiguredModelPath { get; set; } = DefaultModelPath;

    /// <summary>Absolute path resolved at startup.</summary>
    public string ModelPath { get; set; } = DefaultModelPath;

    public string ModelKey { get; set; } = DefaultModelKey;

    /// <summary>Fallback input size when the model declares a dynamic spatial dimension.</summary>
    public int InputSize { get; set; } = 224;
}
