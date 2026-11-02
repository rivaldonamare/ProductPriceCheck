using System.Globalization;
using ElectricalStoreAI.Application.Images;
using ElectricalStoreAI.Application.Products;
using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Infrastructure.ImageStorage;
using ElectricalStoreAI.Infrastructure.Persistence;
using ElectricalStoreAI.Infrastructure.Recognition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElectricalStoreAI.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <param name="contentRootPath">The web host content root; used as a fallback location for the model file.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var dataDirectory = ResolveDataDirectory(configuration, contentRootPath);
        Directory.CreateDirectory(dataDirectory);

        var connectionString = FirstNonEmpty(
                configuration.GetConnectionString("DefaultConnection"),
                configuration["Database:ConnectionString"])
            ?? $"Data Source={Path.Combine(dataDirectory, "ElectricalStoreAI.db")}";

        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

        services.Configure<ImageStorageOptions>(options =>
        {
            var root = FirstNonEmpty(configuration["ImageStorage:RootDirectory"]) ?? "Images";
            options.RootDirectory = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(dataDirectory, root));
            if (long.TryParse(configuration["ImageStorage:MaxFileSizeBytes"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxBytes) && maxBytes > 0)
            {
                options.MaxFileSizeBytes = maxBytes;
            }
        });

        services.Configure<ImageEmbeddingOptions>(options =>
        {
            var configuredPath = FirstNonEmpty(configuration["Recognition:ModelPath"]) ?? ImageEmbeddingOptions.DefaultModelPath;
            options.ConfiguredModelPath = configuredPath;
            options.ModelPath = ResolveModelPath(configuredPath, dataDirectory, contentRootPath);
            options.ModelKey = FirstNonEmpty(configuration["Recognition:ModelKey"]) ?? ImageEmbeddingOptions.DefaultModelKey;
        });

        services.Configure<RecognitionOptions>(options =>
        {
            if (TryGetDouble(configuration, "Recognition:MinTopScore", out var minScore)) options.MinTopScore = minScore;
            if (TryGetDouble(configuration, "Recognition:MinMarginToSecond", out var margin)) options.MinMarginToSecond = margin;
        });

        services.AddSingleton(new AppDataLocations(dataDirectory));
        services.AddSingleton<IImageStorageService, ImageStorageService>();
        services.AddSingleton<OnnxImageEmbeddingService>();
        services.AddSingleton<IImageEmbeddingService>(provider => provider.GetRequiredService<OnnxImageEmbeddingService>());
        services.AddSingleton<EmbeddingVectorCache>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductImageService, ProductImageService>();
        services.AddScoped<IProductRecognitionService, ProductRecognitionService>();

        return services;
    }

    /// <summary>
    /// Absolute DataDirectory is used as-is; a relative one resolves against the content root
    /// (the Web project folder), not the process working directory, so it is stable however the app is started.
    /// Default: %APPDATA%/ElectricalStoreAI.
    /// </summary>
    private static string ResolveDataDirectory(IConfiguration configuration, string contentRootPath)
    {
        var configured = FirstNonEmpty(configuration["DataDirectory"]);
        if (configured is null)
        {
            return Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
                "ElectricalStoreAI"));
        }

        return Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(contentRootPath, configured));
    }

    /// <summary>
    /// A relative model path is searched in: the app-data directory, the content root, the app base directory,
    /// then up to three parent folders of the content root (so `dotnet run` from src/ finds the repo-level Models/).
    /// If nothing exists, the app-data location is returned so the setup error points there.
    /// </summary>
    private static string ResolveModelPath(string configured, string dataDirectory, string contentRootPath)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var candidates = new List<string> { dataDirectory, contentRootPath, AppContext.BaseDirectory };
        var parent = Directory.GetParent(contentRootPath);
        for (var level = 0; level < 3 && parent is not null; level++, parent = parent.Parent)
        {
            candidates.Add(parent.FullName);
        }

        foreach (var baseDirectory in candidates)
        {
            var path = Path.GetFullPath(Path.Combine(baseDirectory, configured));
            if (File.Exists(path))
            {
                return path;
            }
        }

        return Path.GetFullPath(Path.Combine(dataDirectory, configured));
    }

    private static bool TryGetDouble(IConfiguration configuration, string key, out double value)
        => double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

/// <summary>Resolved local data directory (database, images, models).</summary>
public sealed record AppDataLocations(string DataDirectory);
