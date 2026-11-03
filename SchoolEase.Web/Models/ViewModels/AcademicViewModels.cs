using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

// =====================================================================
// Grade Levels
// =====================================================================

/// <summary>Create / edit payload for a grade level.</summary>
public class GradeLevelFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    [Display(Name = "Grade Level")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(10)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Sort Order")]
    public int OrderIndex { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>A grade level together with its related record counts.</summary>
public class GradeLevelRowViewModel
{
    public GradeLevel GradeLevel { get; set; } = null!;
    public int SectionCount { get; set; }
    public int SubjectCount { get; set; }
    public int StudentCount { get; set; }
}

// =====================================================================
// Sections
// =====================================================================

/// <summary>Create / edit payload for a class section.</summary>
public class SectionFormViewModel
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

    [Display(Name = "Class Adviser")]
    public int? AdviserId { get; set; }

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

    public IEnumerable<SelectListItem> GradeLevels { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Advisers { get; set; } = new List<SelectListItem>();
}

/// <summary>Everything the section detail page needs.</summary>
public class SectionDetailsViewModel
{
    public Section Section { get; set; } = null!;
    public List<Student> Students { get; set; } = new();
    public List<TeacherSubjectAssignment> Assignments { get; set; } = new();
    public List<ClassSchedule> Schedules { get; set; } = new();

    public int CapacityPercent =>
        Section.Capacity <= 0 ? 0 : Math.Min(100, (int)Math.Round(Section.StudentCount * 100.0 / Section.Capacity));
}

/// <summary>Student picker used by the "assign students" screen.</summary>
public class AssignStudentsViewModel
{
    public Section Section { get; set; } = null!;
    public List<Student> Students { get; set; } = new();
    public HashSet<int> SelectedIds { get; set; } = new();
}

/// <summary>One subject row on the "assign subjects" screen.</summary>
public class SectionSubjectItem
{
    public Subject Subject { get; set; } = null!;
    public bool Assigned { get; set; }
    public int? TeacherId { get; set; }
}

/// <summary>Subject/teacher matrix used by the "assign subjects" screen.</summary>
public class AssignSubjectsViewModel
{
    public Section Section { get; set; } = null!;
    public List<SectionSubjectItem> Subjects { get; set; } = new();
    public IEnumerable<SelectListItem> Teachers { get; set; } = new List<SelectListItem>();
}

// =====================================================================
// Subjects
// =====================================================================

/// <summary>Create / edit payload for a subject.</summary>
public class SubjectFormViewModel
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

    [Range(0.5, 12, ErrorMessage = "Units must be between 0.5 and 12.")]
    [Display(Name = "Units")]
    public double Units { get; set; } = 1;

    [Display(Name = "Grade Level")]
    public int? GradeLevelId { get; set; }

    [StringLength(80)]
    [Display(Name = "Track / Strand")]
    public string? TrackStrand { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> GradeLevels { get; set; } = new List<SelectListItem>();
}

/// <summary>A subject row enriched with its teacher assignment summary.</summary>
public class SubjectRowViewModel
{
    public Subject Subject { get; set; } = null!;
    public int TeacherCount { get; set; }
    public List<string> TeacherNames { get; set; } = new();
}

/// <summary>Everything the subject detail page needs.</summary>
public class SubjectDetailsViewModel
{
    public Subject Subject { get; set; } = null!;
    public List<TeacherSubjectAssignment> Assignments { get; set; } = new();
    public List<ClassSchedule> Schedules { get; set; } = new();
}

// =====================================================================
// Schedules
// =====================================================================

/// <summary>Create / edit payload for a class schedule entry.</summary>
public class ScheduleFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Section")]
    public int SectionId { get; set; }

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }

    [Display(Name = "Teacher")]
    public int TeacherId { get; set; }

    [StringLength(40)]
    [Display(Name = "Room")]
    public string? Room { get; set; }

    [Display(Name = "Day")]
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    [DataType(DataType.Time)]
    [Display(Name = "Start Time")]
    public TimeSpan StartTime { get; set; } = new(8, 0, 0);

    [DataType(DataType.Time)]
    [Display(Name = "End Time")]
    public TimeSpan EndTime { get; set; } = new(9, 0, 0);

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    public IEnumerable<SelectListItem> Sections { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Subjects { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Teachers { get; set; } = new List<SelectListItem>();
}

/// <summary>A single detected scheduling clash.</summary>
public class ScheduleConflict
{
    public string Message { get; set; } = string.Empty;
    public int FirstId { get; set; }
    public int SecondId { get; set; }
}

/// <summary>Weekly timetable grid.</summary>
public class ScheduleGridViewModel
{
    public List<ClassSchedule> Schedules { get; set; } = new();
    public List<TimeSpan> Slots { get; set; } = new();
    public string? SectionName { get; set; }
    public string? TeacherName { get; set; }

    public List<DayOfWeek> Days { get; } = new()
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    };
}
