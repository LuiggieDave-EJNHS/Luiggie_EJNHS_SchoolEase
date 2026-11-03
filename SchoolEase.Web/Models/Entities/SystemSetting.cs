using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>Editable system setting shown on the System Settings page.</summary>
public class SystemSetting
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Key")]
    public string Key { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Value")]
    public string? Value { get; set; }

    [StringLength(300)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(60)]
    [Display(Name = "Group")]
    public string Group { get; set; } = "General";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }

    [StringLength(120)]
    [Display(Name = "Updated By")]
    public string? UpdatedByName { get; set; }
}
