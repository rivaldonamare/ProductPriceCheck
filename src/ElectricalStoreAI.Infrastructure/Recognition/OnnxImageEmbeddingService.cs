using System.Diagnostics;
using ElectricalStoreAI.Application.Recognition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.Infrastructure.Recognition;

/// <summary>
/// CLIP-style image embedding on CPU with ONNX Runtime. One InferenceSession is shared for the app lifetime
/// (InferenceSession.Run is thread-safe). Preprocessing: EXIF auto-orient, resize shortest side + center crop
/// to the model input size (bicubic), RGB scaled to [0,1] and normalized with the CLIP mean/std, NCHW layout.
/// The output vector is L2-normalized.
/// </summary>
public sealed class OnnxImageEmbeddingService : IImageEmbeddingService, IDisposable
{
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];
    private static readonly string[] PreferredOutputNames = ["image_embeds", "embeddings", "image_embedding", "pooler_output"];

    private readonly ImageEmbeddingOptions _options;
    private readonly ILogger<OnnxImageEmbeddingService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private LoadedModel? _model;
    private volatile bool _isLoading;
    private volatile bool _loadFailed;

    public OnnxImageEmbeddingService(IOptions<ImageEmbeddingOptions> options, ILogger<OnnxImageEmbeddingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ModelKey => _options.ModelKey;

    public EmbeddingModelStatus Status
    {
        get
        {
            if (_model is not null) return EmbeddingModelStatus.Ready;
            if (_isLoading) return EmbeddingModelStatus.Loading;
            if (!File.Exists(_options.ModelPath)) return EmbeddingModelStatus.Missing;
            return _loadFailed ? EmbeddingModelStatus.Failed : EmbeddingModelStatus.NotLoaded;
        }
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        var model = await GetModelAsync(cancellationToken);
        await Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            Run(model, new DenseTensor<float>([1, 3, model.InputSize, model.InputSize]));
            _logger.LogInformation("Embedding model warm-up inference took {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
        }, cancellationToken);
    }

    public async Task<ImageEmbeddingResult> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (imageBytes is not { Length: > 0 })
        {
            throw new AppException("The image is empty.");
        }

        var model = await GetModelAsync(cancellationToken);

        // CPU-bound work off the Blazor circuit's synchronization context.
        return await Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            var tensor = Preprocess(imageBytes, model.InputSize);
            var preprocessing = stopwatch.Elapsed;

            cancellationToken.ThrowIfCancellationRequested();
            stopwatch.Restart();
            var vector = Run(model, tensor);
            return new ImageEmbeddingResult(vector, preprocessing, stopwatch.Elapsed);
        }, cancellationToken);
    }

    public void Dispose()
    {
        _model?.Session.Dispose();
        _initLock.Dispose();
    }

    private async Task<LoadedModel> GetModelAsync(CancellationToken cancellationToken)
    {
        if (_model is { } loaded)
        {
            return loaded;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_model is { } existing)
            {
                return existing;
            }

            if (!File.Exists(_options.ModelPath))
            {
                _logger.LogError("ONNX model file not found at {ModelPath}", _options.ModelPath);
                throw new RecognitionUnavailableException(
                    $"The recognition model file '{_options.ConfiguredModelPath}' was not found. See the About page for setup instructions.");
            }

            _isLoading = true;
            try
            {
                // Not cached on failure: replacing a bad model file works without restarting the app.
                _model = await Task.Run(LoadModel, cancellationToken);
                _loadFailed = false;
                return _model;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _loadFailed = true;
                _logger.LogError(ex, "Failed to load ONNX model {ModelPath}", _options.ModelPath);
                throw new RecognitionUnavailableException(
                    "The recognition model could not be loaded. Check that the model file is a valid image-embedding ONNX model.");
            }
            finally
            {
                _isLoading = false;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private LoadedModel LoadModel()
    {
        var stopwatch = Stopwatch.StartNew();
        using var sessionOptions = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        var session = new InferenceSession(_options.ModelPath, sessionOptions);
        try
        {
            var imageInputs = session.InputMetadata
                .Where(input => input.Value.ElementType == typeof(float) && input.Value.Dimensions.Length == 4)
                .OrderByDescending(input => input.Key == "pixel_values")
                .ToList();

            if (imageInputs.Count == 0 || session.InputMetadata.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one 4D float image input, found: {string.Join(", ", session.InputMetadata.Keys)}");
            }

            var input = imageInputs[0];
            var declaredSize = input.Value.Dimensions[2];
            var inputSize = declaredSize > 0 ? declaredSize : _options.InputSize;

            var outputName = session.OutputMetadata.Keys.FirstOrDefault(name => PreferredOutputNames.Contains(name))
                ?? session.OutputMetadata.Where(output => output.Value.Dimensions.Length == 2).Select(output => output.Key).FirstOrDefault()
                ?? session.OutputMetadata.Keys.First();

            _logger.LogInformation(
                "Loaded ONNX model {ModelKey} in {ElapsedMs} ms (input {InputName} {InputSize}x{InputSize}, output {OutputName})",
                _options.ModelKey, stopwatch.ElapsedMilliseconds, input.Key, inputSize, inputSize, outputName);

            return new LoadedModel(session, input.Key, outputName, inputSize);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static DenseTensor<float> Preprocess(byte[] imageBytes, int size)
    {
        Image<Rgb24> image;
        try
        {
            image = Image.Load<Rgb24>(imageBytes);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            throw new AppException("The image could not be read. Use a JPEG, PNG, WebP or GIF photo.");
        }

        using (image)
        {
            image.Mutate(context => context
                .AutoOrient()
                .Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Crop,
                    Sampler = KnownResamplers.Bicubic,
                }));

            var tensor = new DenseTensor<float>([1, 3, size, size]);
            var buffer = tensor.Buffer;
            var plane = size * size;

            image.ProcessPixelRows(accessor =>
            {
                var span = buffer.Span;
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var offset = y * size;
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        span[offset + x] = (pixel.R / 255f - Mean[0]) / Std[0];
                        span[plane + offset + x] = (pixel.G / 255f - Mean[1]) / Std[1];
                        span[(2 * plane) + offset + x] = (pixel.B / 255f - Mean[2]) / Std[2];
                    }
                }
            });

            return tensor;
        }
    }

    private static float[] Run(LoadedModel model, DenseTensor<float> tensor)
    {
        using var results = model.Session.Run(
            [NamedOnnxValue.CreateFromTensor(model.InputName, tensor)],
            [model.OutputName]);

        var output = results[0].AsTensor<float>();
        float[] vector = output.Dimensions.Length == 3
            ? output.ToArray().AsSpan(0, output.Dimensions[2]).ToArray() // [batch, tokens, dim] -> CLS token
            : output.ToArray();

        var norm = MathF.Sqrt(vector.Sum(value => value * value));
        if (norm == 0 || float.IsNaN(norm))
        {
            throw new RecognitionUnavailableException("The recognition model returned an invalid embedding.");
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return vector;
    }

    private sealed record LoadedModel(InferenceSession Session, string InputName, string OutputName, int InputSize);
}
