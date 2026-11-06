namespace SchoolEase.Web.Services;

/// <summary>
/// Stores uploaded documents outside the database. Implementations must be safe
/// to call when no bucket is configured: <see cref="IsConfigured"/> is false and
/// the caller falls back to keeping the bytes in the database.
/// </summary>
public interface IFileStorage
{
    /// <summary>True when a remote bucket is configured and reachable settings exist.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads the content and returns the key to store on the record.</summary>
    Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Downloads the content previously stored under <paramref name="key"/>.</summary>
    Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Removes an object. Missing objects are ignored.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
