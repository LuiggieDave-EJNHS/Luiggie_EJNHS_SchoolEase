using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// Immutable record of a security-relevant action. Written by <c>AuditService</c>
/// for logins, CRUD operations and document-request workflow changes.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    [StringLength(450)]
    [Display(Name = "User")]
    public string? UserId { get; set; }

    [StringLength(120)]
    [Display(Name = "Performed By")]
    public string? UserName { get; set; }

    [Required, StringLength(60)]
    [Display(Name = "Action")]
    public string Action { get; set; } = string.Empty;

    [StringLength(60)]
    [Display(Name = "Module")]
    public string? EntityName { get; set; }

    [StringLength(60)]
    public string? EntityId { get; set; }

    [StringLength(1000)]
    [Display(Name = "Details")]
    public string? Details { get; set; }

    [StringLength(60)]
    [Display(Name = "IP Address")]
    public string? IpAddress { get; set; }

    [Display(Name = "Timestamp")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
