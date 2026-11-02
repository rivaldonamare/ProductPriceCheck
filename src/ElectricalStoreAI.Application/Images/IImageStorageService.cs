namespace ElectricalStoreAI.Application.Images;

/// <summary>A validated image written to local storage. ContentType is derived from the file signature, never from the client.</summary>
public sealed record StoredImage(string StorageKey, string ContentType, byte[] Content);

public interface IImageStorageService
{
    long MaxFileSizeBytes { get; }

    /// <summary>Validates (size + signature) and stores the image under a server-generated name.</summary>
    Task<StoredImage> SaveAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>Returns the stored bytes, or null when the file no longer exists.</summary>
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
