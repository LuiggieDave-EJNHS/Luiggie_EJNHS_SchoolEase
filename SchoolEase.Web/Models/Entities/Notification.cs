using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>An in-app notification delivered to a single user's bell menu.</summary>
public class Notification
{
    public int Id { get; set; }

    [Display(Name = "Recipient")]
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    [Required, StringLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(600)]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "Type")]
    public NotificationType Type { get; set; } = NotificationType.Info;

    /// <summary>Optional in-app deep link (e.g. /DocumentRequests/Details/5).</summary>
    [StringLength(300)]
    public string? Url { get; set; }

    [Display(Name = "Read")]
    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Icon => Type switch
    {
        NotificationType.Success => "bi-check-circle-fill",
        NotificationType.Warning => "bi-exclamation-triangle-fill",
        NotificationType.Danger => "bi-x-octagon-fill",
        _ => "bi-info-circle-fill"
    };

    public string ToneCss => Type switch
    {
        NotificationType.Success => "success",
        NotificationType.Warning => "warning",
        NotificationType.Danger => "danger",
        _ => "primary"
    };
}
