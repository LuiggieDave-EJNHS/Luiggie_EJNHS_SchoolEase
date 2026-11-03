using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A course / learning area offered by the school.</summary>
public class Subject
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Subject Code")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [Display(Name = "Subject Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [Range(0.5, 12)]
    [Display(Name = "Units")]
    public double Units { get; set; } = 1;

    [Display(Name = "Grade Level")]
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }

    [StringLength(80)]
    [Display(Name = "Track / Strand")]
    public string? TrackStrand { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string DisplayName => $"{Code} - {Title}";

    public ICollection<TeacherSubjectAssignment> TeacherAssignments { get; set; } = new List<TeacherSubjectAssignment>();
    public ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
}
