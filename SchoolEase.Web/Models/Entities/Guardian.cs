using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>Parent or legal guardian of one or more students.</summary>
public class Guardian
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required, StringLength(40)]
    [Display(Name = "Relationship")]
    public string Relationship { get; set; } = "Parent";

    [StringLength(30)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(80)]
    public string? Occupation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    [Display(Name = "Guardian Name")]
    public string FullName => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
