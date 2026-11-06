using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A non-teaching (office / support) personnel record.</summary>
public class Staff
{
    public int Id { get; set; }

    [Display(Name = "Linked Account")]
    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Staff ID")]
    public string StaffNumber { get; set; } = string.Empty;

    [Required, StringLength(80)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Sex")]
    public Gender Gender { get; set; } = Gender.Male;

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime? BirthDate { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    [RegularExpression(@"^[0-9+\-() ]*$", ErrorMessage = "Use numbers only (digits, plus sign, dashes and parentheses are allowed).")]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Position")]
    public string Position { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Department / Office")]
    public string? Department { get; set; }

    [Display(Name = "Employment Status")]
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Permanent;

    [DataType(DataType.Date)]
    [Display(Name = "Date Hired")]
    public DateTime? DateHired { get; set; }

    public string? PhotoPath { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    [Display(Name = "Full Name")]
    public string FullName => string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
