using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Models.ViewModels;

public class ReportColumn
{
    public string Header { get; set; } = string.Empty;
    public string Align { get; set; } = "left";
    public string Width { get; set; } = string.Empty;
}

public class ReportRow
{
    public List<string> Cells { get; set; } = new();
    public string? Tone { get; set; }

    public ReportRow() { }

    public ReportRow(params string?[] cells)
    {
        Cells = cells.Select(c => c ?? string.Empty).ToList();
    }
}

/// <summary>
/// Engine-agnostic report payload. Every printable/downloadable report in the
/// system is shaped into this structure so the print layout and the CSV export
/// can be shared.
/// </summary>
public class ReportResult
{
    public string Title { get; set; } = "Report";
    public string Subtitle { get; set; } = string.Empty;
    public string FilterSummary { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;

    public List<ReportColumn> Columns { get; set; } = new();
    public List<ReportRow> Rows { get; set; } = new();
    public Dictionary<string, string> Summary { get; set; } = new();

    /// <summary>Optional extra table rendered under the main one (e.g. per-section subtotals).</summary>
    public string? Note { get; set; }

    public bool IsEmpty => Rows.Count == 0;

    public string CsvFileName =>
        $"{Title.Replace(' ', '_')}_{GeneratedAt:yyyyMMdd_HHmm}.csv";
}

/// <summary>Filter values posted from the Reports page.</summary>
public class ReportFilterViewModel
{
    public string ReportType { get; set; } = "student-list";

    public DateTime? DateFrom { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime? DateTo { get; set; } = DateTime.Today;

    public int? GradeLevelId { get; set; }
    public int? SectionId { get; set; }
    public int? SubjectId { get; set; }
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
    public string? SchoolYear { get; set; } = SchoolInfo.SchoolYear;
    public int? Term { get; set; }
    public string? Status { get; set; }
    public string? Keyword { get; set; }

    // Select lists (populated by the controller)
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> GradeLevels { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Sections { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Subjects { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Teachers { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Students { get; set; } = new();
}
