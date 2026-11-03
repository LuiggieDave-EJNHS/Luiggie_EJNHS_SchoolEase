using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A class section, e.g. "12-ICT-CCS" under Grade 12.</summary>
public class Section
{
    public int Id { get; set; }

    [Required, StringLength(60)]
    [Display(Name = "Section Name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(30)]
    [Display(Name = "Section Code")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Grade Level")]
    public int GradeLevelId { get; set; }
    public GradeLevel GradeLevel { get; set; } = null!;

    [Display(Name = "Class Adviser")]
    public int? AdviserId { get; set; }
    public Teacher? Adviser { get; set; }

    [StringLength(80)]
    [Display(Name = "Track / Strand")]
    public string? TrackStrand { get; set; }

    [StringLength(40)]
    [Display(Name = "Room")]
    public string? Room { get; set; }

    [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
    [Display(Name = "Capacity")]
    public int Capacity { get; set; } = 40;

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    [Display(Name = "Enrolled Students")]
    public int StudentCount { get; set; }

    [NotMapped]
    public string DisplayName => $"{GradeLevel?.Name} - {Name}";

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
