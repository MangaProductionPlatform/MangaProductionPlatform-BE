namespace MangaERP.BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// Abstraction for S3-compatible object storage (AWS S3 / Cloudflare R2).
/// </summary>
public interface IS3StorageService
{
    /// <summary>Uploads a file stream and returns the public URL.</summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file by its key.</summary>
    Task DeleteAsync(string fileKey, CancellationToken cancellationToken = default);

    /// <summary>Generates a pre-signed URL for temporary access.</summary>
    string GeneratePresignedUrl(string fileKey, TimeSpan expiry);
}
