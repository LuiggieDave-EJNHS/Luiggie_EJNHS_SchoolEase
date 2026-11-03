using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// Identity user extended with school-specific profile fields.
/// Every student, teacher and staff member is optionally linked to one of these accounts.
/// </summary>
public class AppUser : IdentityUser
{
    [Required, StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Profile Photo")]
    public string? ProfilePicturePath { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Last Login")]
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
