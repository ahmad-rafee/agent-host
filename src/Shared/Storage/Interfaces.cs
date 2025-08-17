namespace AgentHost.Shared.Storage;

public interface IBlobStorage
{
    Task<string> PutObjectAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> GetObjectAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteObjectAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}

public record BlobMetadata(
    string Key,
    long Size,
    string ContentType,
    string? ETag = null,
    DateTimeOffset LastModified = default);

public class BlobStorageException : Exception
{
    public BlobStorageException(string message) : base(message) { }
    public BlobStorageException(string message, Exception innerException) : base(message, innerException) { }
}
