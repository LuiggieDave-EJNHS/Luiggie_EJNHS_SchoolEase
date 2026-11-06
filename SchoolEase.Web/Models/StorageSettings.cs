namespace SchoolEase.Web.Models;

/// <summary>
/// Optional S3-compatible object storage (Cloudflare R2 works on the free tier).
/// When <see cref="AccessKey"/> and <see cref="SecretKey"/> are empty the
/// application stores uploaded files in the database instead, so the feature
/// stays entirely optional.
/// </summary>
public class StorageSettings
{
    /// <summary>Cloudflare account id; the S3 endpoint becomes https://{AccountId}.r2.cloudflarestorage.com.</summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>Overrides <see cref="AccountId"/> for any other S3-compatible provider.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Key prefix so several environments can share one bucket.</summary>
    public string Prefix { get; set; } = "schoolease";

    /// <summary>True when enough settings are present to talk to the bucket.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(Bucket)
        && (!string.IsNullOrWhiteSpace(AccountId) || !string.IsNullOrWhiteSpace(ServiceUrl));

    /// <summary>The S3 endpoint to use.</summary>
    public string Endpoint =>
        !string.IsNullOrWhiteSpace(ServiceUrl)
            ? ServiceUrl.Trim()
            : $"https://{AccountId.Trim()}.r2.cloudflarestorage.com";
}
