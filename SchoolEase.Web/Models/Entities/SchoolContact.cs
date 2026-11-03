using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>Contact information for other schools, used by registrars for inter-school coordination.</summary>
public class SchoolContact
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "School Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "School ID")]
    public string? SchoolId { get; set; }

    [StringLength(100)]
    public string? Division { get; set; }

    [StringLength(100)]
    public string? Region { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [StringLength(100)]
    [Display(Name = "Registrar Name")]
    public string? RegistrarName { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(400)]
    public string? Remarks { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
