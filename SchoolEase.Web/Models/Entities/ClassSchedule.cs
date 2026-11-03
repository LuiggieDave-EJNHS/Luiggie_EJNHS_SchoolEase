using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A single recurring class meeting in the weekly timetable.</summary>
public class ClassSchedule
{
    public int Id { get; set; }

    [Display(Name = "Section")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "Teacher")]
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    [StringLength(40)]
    [Display(Name = "Room")]
    public string? Room { get; set; }

    [Display(Name = "Day")]
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    [DataType(DataType.Time)]
    [Display(Name = "Start Time")]
    public TimeSpan StartTime { get; set; }

    [DataType(DataType.Time)]
    [Display(Name = "End Time")]
    public TimeSpan EndTime { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string TimeRange => $"{DateTime.Today.Add(StartTime):h:mm tt} - {DateTime.Today.Add(EndTime):h:mm tt}";

    [NotMapped]
    public double DurationHours => (EndTime - StartTime).TotalHours;
}
