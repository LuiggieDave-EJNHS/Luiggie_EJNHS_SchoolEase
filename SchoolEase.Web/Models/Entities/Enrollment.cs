using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>Per-school-year enrollment record of a student in a section.</summary>
public class Enrollment
{
    public int Id { get; set; }

    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "Grade Level")]
    public int GradeLevelId { get; set; }
    public GradeLevel GradeLevel { get; set; } = null!;

    [Display(Name = "Section")]
    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    [Display(Name = "Status")]
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    [DataType(DataType.Date)]
    [Display(Name = "Date Enrolled")]
    public DateTime DateEnrolled { get; set; } = DateTime.UtcNow;

    [StringLength(300)]
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
