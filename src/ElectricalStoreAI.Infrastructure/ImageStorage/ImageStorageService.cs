using System.Text.RegularExpressions;
using ElectricalStoreAI.Application.Images;
using Microsoft.Extensions.Options;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.Infrastructure.ImageStorage;

public sealed class ImageStorageOptions
{
    /// <summary>Absolute directory for reference images (resolved under the app-data directory at startup).</summary>
    public string RootDirectory { get; set; } = "Images";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>
/// Stores reference images as files named by a server-generated GUID. The storage key is only the file name;
/// it is validated against a strict pattern before any filesystem access, so keys can never escape the root.
/// </summary>
public sealed partial class ImageStorageService : IImageStorageService
{
    private readonly string _rootDirectory;

    public ImageStorageService(IOptions<ImageStorageOptions> options)
    {
        _rootDirectory = Path.GetFullPath(options.Value.RootDirectory);
        MaxFileSizeBytes = options.Value.MaxFileSizeBytes;
    }

    public long MaxFileSizeBytes { get; }

    public async Task<StoredImage> SaveAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = await ReadBoundedAsync(content, cancellationToken);
        if (bytes.Length == 0)
        {
            throw new AppException("The uploaded file is empty.");
        }

        var format = DetectFormat(bytes)
            ?? throw new AppException("The uploaded file is not a supported image. Use JPEG, PNG, WebP or GIF.");

        var storageKey = $"{Guid.NewGuid():N}{format.Extension}";
        Directory.CreateDirectory(_rootDirectory);
        await File.WriteAllBytesAsync(Path.Combine(_rootDirectory, storageKey), bytes, cancellationToken);

        return new StoredImage(storageKey, format.ContentType, bytes);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        return File.Exists(fullPath) ? await File.ReadAllBytesAsync(fullPath, cancellationToken) : null;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Task.CompletedTask;
        }

        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private async Task<byte[]> ReadBoundedAsync(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxFileSizeBytes)
            {
                throw new AppException($"The image is too large. The maximum size is {MaxFileSizeBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private string GetFullPath(string storageKey)
    {
        // Legacy rows stored "Images/<file>"; accept that prefix but nothing else.
        var match = StorageKeyPattern().Match(storageKey ?? string.Empty);
        if (!match.Success)
        {
            throw new AppException("The image reference is invalid.");
        }

        return Path.Combine(_rootDirectory, match.Groups["file"].Value);
    }

    private static (string Extension, string ContentType)? DetectFormat(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
        {
            return (".png", "image/png");
        }

        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8')
        {
            return (".gif", "image/gif");
        }

        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    [GeneratedRegex(@"^(?:Images/)?(?<file>[0-9a-f]{32}\.(?:jpg|png|gif|webp))$", RegexOptions.CultureInvariant)]
    private static partial Regex StorageKeyPattern();
}
