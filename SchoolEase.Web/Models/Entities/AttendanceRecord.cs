using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>One attendance mark for a student on a given day (optionally per subject).</summary>
public class AttendanceRecord
{
    public int Id { get; set; }

    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "Section")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "Subject")]
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Display(Name = "Status")]
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    [StringLength(200)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    [Display(Name = "Recorded By")]
    public string? RecordedByUserId { get; set; }
    public AppUser? RecordedBy { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
