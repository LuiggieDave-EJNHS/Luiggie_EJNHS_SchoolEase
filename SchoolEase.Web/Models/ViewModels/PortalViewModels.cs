using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

// ==========================================================================
// Teacher portal
// ==========================================================================

/// <summary>The editable subset of a teacher's own record.</summary>
public class TeacherProfileEditViewModel
{
    [StringLength(30)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }

    [EmailAddress, StringLength(120)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(250)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(120)]
    [Display(Name = "Specialization")]
    public string? Specialization { get; set; }
}

/// <summary>Read-mostly teacher profile page.</summary>
public class TeacherProfileViewModel
{
    public Teacher? Teacher { get; set; }
    public int AdvisoryCount { get; set; }
    public int AssignedSubjectCount { get; set; }
    public int AssignedSectionCount { get; set; }
    public TeacherProfileEditViewModel Form { get; set; } = new();
}

/// <summary>One section the teacher handles, with the subjects taught inside it.</summary>
public class TeacherClassCard
{
    public Section Section { get; set; } = null!;
    public bool IsAdviser { get; set; }
    public int StudentCount { get; set; }
    public List<Subject> Subjects { get; set; } = new();
}

public class TeacherClassesViewModel
{
    public Teacher? Teacher { get; set; }
    public List<TeacherClassCard> Classes { get; set; } = new();
}

public class TeacherScheduleViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public List<ClassSchedule> Entries { get; set; } = new();
    public double WeeklyHours { get; set; }
}

// ==========================================================================
// Student portal
// ==========================================================================

public class StudentScheduleViewModel
{
    public Student? Student { get; set; }
    public bool HasSection { get; set; }
    public List<ClassSchedule> Weekly { get; set; } = new();
    public List<ClassSchedule> Today { get; set; } = new();
}

public class StudentAttendanceViewModel
{
    public Student? Student { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }

    public int PresentDays { get; set; }
    public int LateDays { get; set; }
    public int AbsentDays { get; set; }
    public int ExcusedDays { get; set; }
    public double AttendanceRate { get; set; }

    public ChartData Trend { get; set; } = new() { Title = "My Attendance (Last 7 Days)" };
    public PagedList<AttendanceRecord> Records { get; set; } =
        new(new List<AttendanceRecord>(), 0, 1, 15);

    public List<SelectListItem> Months { get; set; } = new();
    public List<SelectListItem> Years { get; set; } = new();
}

/// <summary>One subject row on the student's report card.</summary>
public class StudentSubjectGradeRow
{
    public Subject Subject { get; set; } = null!;
    public Dictionary<int, double?> Terms { get; set; } = new();
    public double? Average { get; set; }

    public string Remark => Average.GradeRemarks();
}

public class MyGradesViewModel
{
    public Student? Student { get; set; }
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;
    public double? GeneralAverage { get; set; }
    public List<StudentSubjectGradeRow> Subjects { get; set; } = new();
}

// ==========================================================================
// Reports
// ==========================================================================

/// <summary>The Reports page state: the filter, plus an optional generated payload.</summary>
public class ReportPageViewModel
{
    public ReportFilterViewModel Filter { get; set; } = new();
    public ReportResult? Report { get; set; }
    public bool IsPrint { get; set; }

    /// <summary>Flattens the active filter into route values for the Print / Export links.</summary>
    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["ReportType"] = Filter.ReportType };

        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) values[key] = value;
        }

        Add("DateFrom", Filter.DateFrom?.ToString("yyyy-MM-dd"));
        Add("DateTo", Filter.DateTo?.ToString("yyyy-MM-dd"));
        Add("GradeLevelId", Filter.GradeLevelId is > 0 ? Filter.GradeLevelId.Value.ToString() : null);
        Add("SectionId", Filter.SectionId is > 0 ? Filter.SectionId.Value.ToString() : null);
        Add("SubjectId", Filter.SubjectId is > 0 ? Filter.SubjectId.Value.ToString() : null);
        Add("StudentId", Filter.StudentId is > 0 ? Filter.StudentId.Value.ToString() : null);
        Add("TeacherId", Filter.TeacherId is > 0 ? Filter.TeacherId.Value.ToString() : null);
        Add("SchoolYear", Filter.SchoolYear);
        Add("Term", Filter.Term is > 0 ? Filter.Term.Value.ToString() : null);
        Add("Status", Filter.Status);
        Add("Keyword", Filter.Keyword);

        return values;
    }
}
