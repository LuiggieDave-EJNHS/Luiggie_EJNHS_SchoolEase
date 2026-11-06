using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

// ===========================================================================
// Attendance
// ===========================================================================

/// <summary>Administrator view of the attendance register (filter + table + charts).</summary>
public class AttendanceIndexViewModel
{
    public PagedList<AttendanceRecord> Records { get; set; } = new(new List<AttendanceRecord>(), 0, 1, 20);

    // Active filters
    public int? SectionId { get; set; }
    public int? GradeLevelId { get; set; }
    public DateTime? Date { get; set; }
    public AttendanceStatus? Status { get; set; }
    public int? StudentId { get; set; }

    // Summary of the filtered set
    public int TotalCount { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int ExcusedCount { get; set; }
    public double AttendanceRate { get; set; }

    public ChartData StatusDistribution { get; set; } = new() { Title = "Attendance Status Distribution" };
    public ChartData DailyTrend { get; set; } = new() { Title = "Daily Attendance Trend (Last 14 Days)" };

    public List<SelectListItem> Sections { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
    public List<SelectListItem> Students { get; set; } = new();
}

/// <summary>Teacher marking sheet: who is in the section and how they were marked.</summary>
public class AttendanceMarkViewModel
{
    public int SectionId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string SectionLabel { get; set; } = string.Empty;

    public List<AttendanceSheetRow> Rows { get; set; } = new();
    public List<SelectListItem> Sections { get; set; } = new();
}

public class AttendanceSheetRow
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }
}

/// <summary>Posted back from the marking sheet, one per student.</summary>
public class AttendanceRowInput
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Remarks { get; set; }
}

// ===========================================================================
// Grades
// ===========================================================================

/// <summary>Overview table of encoded grade records with summary statistics.</summary>
public class GradeIndexViewModel
{
    public PagedList<GradeRecord> Records { get; set; } = new(new List<GradeRecord>(), 0, 1, 20);

    // Active filters
    public int? SectionId { get; set; }
    public int? SubjectId { get; set; }
    public int? Term { get; set; }
    public int? GradeLevelId { get; set; }

    // Summary of the filtered set
    public int TotalRecords { get; set; }
    public int GradedCount { get; set; }
    public double ClassAverage { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }

    public ChartData SubjectAverages { get; set; } = new() { Title = "Average Final Grade per Subject" };

    public List<SelectListItem> Sections { get; set; } = new();
    public List<SubjectOption> Subjects { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
}

/// <summary>Grade encoding sheet for one section + subject + term.</summary>
public class GradeEncodeViewModel
{
    public int SectionId { get; set; }
    public int SubjectId { get; set; }
    public int Term { get; set; } = 1;

    public string SectionLabel { get; set; } = string.Empty;
    public string SubjectLabel { get; set; } = string.Empty;

    public List<GradeEncodeRow> Rows { get; set; } = new();

    public List<SelectListItem> Sections { get; set; } = new();
    public List<SelectListItem> Subjects { get; set; } = new();
}

public class GradeEncodeRow
{
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public double? WrittenWork { get; set; }
    public double? PerformanceTask { get; set; }
    public double? TermAssessment { get; set; }
    public double? FinalGrade { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>Posted back from the encoding sheet, one per student.</summary>
public class GradeRowInput
{
    public int StudentId { get; set; }
    public double? WrittenWork { get; set; }
    public double? PerformanceTask { get; set; }
    public double? TermAssessment { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>Printable academic record for a single learner.</summary>
public class StudentGradesViewModel
{
    public Student Student { get; set; } = null!;
    public string StudentName { get; set; } = string.Empty;
    public string? ClassLabel { get; set; }
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;
    public double? GeneralAverage { get; set; }

    public List<SubjectGradeGroup> Subjects { get; set; } = new();
}

public class SubjectGradeGroup
{
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectTitle { get; set; } = string.Empty;

    /// <summary>Term number mapped to the encoded final grade (null when not yet graded).</summary>
    public Dictionary<int, double?> Terms { get; set; } = new();

    public double? Average { get; set; }
}

/// <summary>Printable class record sheet for a section + subject + term.</summary>
public class GradePrintViewModel
{
    public string SectionLabel { get; set; } = string.Empty;
    public string SubjectLabel { get; set; } = string.Empty;
    public int Term { get; set; } = 1;
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public double? ClassAverage { get; set; }

    public List<GradeEncodeRow> Rows { get; set; } = new();
}
