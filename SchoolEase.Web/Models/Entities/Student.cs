using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A learner enrolled in the senior high school program.</summary>
public class Student
{
    public int Id { get; set; }

    /// <summary>Optional link to a login account (role: Student).</summary>
    [Display(Name = "Linked Account")]
    public string? UserId { get; set; }
    public AppUser? User { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "Student Number")]
    public string StudentNumber { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "LRN")]
    public string? Lrn { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required, StringLength(80)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(10)]
    public string? Suffix { get; set; }

    [Display(Name = "Sex")]
    public Gender Gender { get; set; } = Gender.Male;

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime? BirthDate { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(30)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [EmailAddress, StringLength(120)]
    public string? Email { get; set; }

    [Display(Name = "Guardian")]
    public int? GuardianId { get; set; }
    public Guardian? Guardian { get; set; }

    [Display(Name = "Grade Level")]
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }

    [Display(Name = "Section")]
    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    [Display(Name = "Enrollment Status")]
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Enrolled;

    [DataType(DataType.Date)]
    [Display(Name = "Date Enrolled")]
    public DateTime DateEnrolled { get; set; } = DateTime.UtcNow;

    public string? PhotoPath { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    [Display(Name = "Full Name")]
    public string FullName =>
        string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
        + (string.IsNullOrWhiteSpace(Suffix) ? string.Empty : $" {Suffix}");

    /// <summary>Convenience label such as "Grade 12 - 12-ICT-CCS".</summary>
    [NotMapped]
    public string ClassLabel =>
        GradeLevel is null ? "Unassigned" : $"{GradeLevel.Name} - {Section?.Name ?? "No Section"}";

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<GradeRecord> GradeRecords { get; set; } = new List<GradeRecord>();
    public ICollection<DocumentRequest> DocumentRequests { get; set; } = new List<DocumentRequest>();
}
