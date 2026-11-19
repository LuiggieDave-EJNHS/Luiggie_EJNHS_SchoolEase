using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A user's profile photo. Kept in the database rather than object storage: photos
/// are small, and a row per user keeps them available on every host and across
/// redeploys without needing a bucket. Served through the profile picture controller.
/// </summary>
public class UserPhoto
{
    [Key, StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    public AppUser? User { get; set; }

    public byte[] Content { get; set; } = Array.Empty<byte>();

    [Required, StringLength(120)]
    public string ContentType { get; set; } = "image/png";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
