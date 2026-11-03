using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// Links a teacher to a subject within a specific section for a school year.
/// This is how "assign subjects" and "assign sections" are stored.
/// </summary>
public class TeacherSubjectAssignment
{
    public int Id { get; set; }

    [Display(Name = "Teacher")]
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "Section")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Label => $"{Teacher?.FullName} • {Subject?.Code} • {Section?.Name}";
}
