using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.ViewModels;

namespace SchoolEase.Web.Services;

/// <summary>Produces the printable / downloadable report payloads.</summary>
public interface IReportService
{
    Task<ReportResult> BuildAsync(ReportFilterViewModel filter, string generatedBy);
    Task<string> BuildStudentsCsvAsync(ReportFilterViewModel filter, string generatedBy);
    Task<string> BuildAttendanceCsvAsync(ReportFilterViewModel filter, string generatedBy);
}

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db) => _db = db;

    public async Task<ReportResult> BuildAsync(ReportFilterViewModel filter, string generatedBy) =>
        filter.ReportType switch
        {
            "attendance" => await AttendanceReportAsync(filter, generatedBy),
            "grades" => await GradeReportAsync(filter, generatedBy),
            "enrollment" => await EnrollmentReportAsync(filter, generatedBy),
            "requests" => await DocumentRequestReportAsync(filter, generatedBy),
            "teacher-schedule" => await TeacherScheduleReportAsync(filter, generatedBy),
            "class-list" => await ClassListReportAsync(filter, generatedBy),
            _ => await StudentListReportAsync(filter, generatedBy)
        };

    // ------------------------------------------------------------------
    public async Task<string> BuildStudentsCsvAsync(ReportFilterViewModel filter, string generatedBy) =>
        ToCsv(await StudentListReportAsync(filter, generatedBy));

    public async Task<string> BuildAttendanceCsvAsync(ReportFilterViewModel filter, string generatedBy) =>
        ToCsv(await AttendanceReportAsync(filter, generatedBy));

    /// <summary>Serialises any report payload to CSV (Excel friendly).</summary>
    public static string ToCsv(ReportResult report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(report.Title);
        if (!string.IsNullOrWhiteSpace(report.Subtitle)) sb.AppendLine(report.Subtitle);
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm} by {report.GeneratedBy}");
        if (!string.IsNullOrWhiteSpace(report.FilterSummary)) sb.AppendLine(report.FilterSummary);
        sb.AppendLine();

        sb.AppendLine(string.Join(',', report.Columns.Select(c => Escape(c.Header))));

        foreach (var row in report.Rows)
        {
            sb.AppendLine(string.Join(',', row.Cells.Select(Escape)));
        }

        if (report.Summary.Count > 0)
        {
            sb.AppendLine();
            foreach (var pair in report.Summary)
            {
                sb.AppendLine($"{Escape(pair.Key)},{Escape(pair.Value)}");
            }
        }

        return sb.ToString();

        static string Escape(string? value)
        {
            value ??= string.Empty;
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }

    // ------------------------------------------------------------------
    // 1. Student list
    // ------------------------------------------------------------------
    private async Task<ReportResult> StudentListReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var query = _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .Include(s => s.Guardian)
            .AsQueryable();

        if (f.GradeLevelId is > 0) query = query.Where(s => s.GradeLevelId == f.GradeLevelId);
        if (f.SectionId is > 0) query = query.Where(s => s.SectionId == f.SectionId);
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim();
            query = query.Where(s => s.FirstName.Contains(k) || s.LastName.Contains(k) || s.StudentNumber.Contains(k));
        }

        var students = await query
            .OrderBy(s => s.GradeLevel!.OrderIndex).ThenBy(s => s.Section!.Name).ThenBy(s => s.LastName)
            .ToListAsync();

        var report = new ReportResult
        {
            Title = "Master List of Students",
            Subtitle = $"{SchoolInfo.SchoolName} • School Year {f.SchoolYear}",
            FilterSummary = BuildFilterSummary(f, ("Grade Level", await GradeLabelAsync(f.GradeLevelId)), ("Section", await SectionLabelAsync(f.SectionId))),
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "No." },
                new() { Header = "Student No." },
                new() { Header = "Name" },
                new() { Header = "Sex" },
                new() { Header = "Grade & Section" },
                new() { Header = "Contact No." },
                new() { Header = "Guardian" },
                new() { Header = "Status", Align = "center" }
            }
        };

        var index = 1;
        foreach (var s in students)
        {
            report.Rows.Add(new ReportRow(
                (index++).ToString(),
                s.StudentNumber,
                s.FullName,
                s.Gender.DisplayName(),
                s.ClassLabel,
                s.ContactNumber ?? "-",
                s.Guardian?.FullName ?? "-",
                s.EnrollmentStatus.DisplayName()));
        }

        report.Summary["Total Students"] = students.Count.ToString();
        report.Summary["Male"] = students.Count(s => s.Gender == Gender.Male).ToString();
        report.Summary["Female"] = students.Count(s => s.Gender == Gender.Female).ToString();
        report.Summary["Officially Enrolled"] = students.Count(s => s.EnrollmentStatus == EnrollmentStatus.Enrolled).ToString();

        return report;
    }

    // ------------------------------------------------------------------
    // 2. Attendance
    // ------------------------------------------------------------------
    private async Task<ReportResult> AttendanceReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var from = (f.DateFrom ?? DateTime.Today.AddDays(-30)).Date;
        var to = (f.DateTo ?? DateTime.Today).Date;

        var query = _db.AttendanceRecords
            .Include(a => a.Student).ThenInclude(s => s.GradeLevel)
            .Include(a => a.Student).ThenInclude(s => s.Section)
            .Where(a => a.Date >= from && a.Date < to.AddDays(1));

        if (f.SectionId is > 0) query = query.Where(a => a.SectionId == f.SectionId);
        if (f.StudentId is > 0) query = query.Where(a => a.StudentId == f.StudentId);
        if (f.GradeLevelId is > 0) query = query.Where(a => a.Student!.GradeLevelId == f.GradeLevelId);

        var records = await query.OrderBy(a => a.Date).ThenBy(a => a.Student!.LastName).ToListAsync();

        var report = new ReportResult
        {
            Title = "Attendance Report",
            Subtitle = $"{SchoolInfo.SchoolName} • {from:MMM d, yyyy} to {to:MMM d, yyyy}",
            FilterSummary = BuildFilterSummary(f, ("Section", await SectionLabelAsync(f.SectionId)), ("Grade Level", await GradeLabelAsync(f.GradeLevelId))),
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "Date" },
                new() { Header = "Student No." },
                new() { Header = "Student Name" },
                new() { Header = "Grade & Section" },
                new() { Header = "Status", Align = "center" },
                new() { Header = "Remarks" }
            }
        };

        foreach (var r in records)
        {
            report.Rows.Add(new ReportRow(
                r.Date.ToDateLabel(),
                r.Student?.StudentNumber ?? "-",
                r.Student?.FullName ?? "-",
                r.Student?.ClassLabel ?? "-",
                r.Status.DisplayName(),
                r.Remarks ?? "-"));
        }

        var total = records.Count;
        report.Summary["Total Marks"] = total.ToString();
        report.Summary["Present"] = records.Count(r => r.Status == AttendanceStatus.Present).ToString();
        report.Summary["Late"] = records.Count(r => r.Status == AttendanceStatus.Late).ToString();
        report.Summary["Absent"] = records.Count(r => r.Status == AttendanceStatus.Absent).ToString();
        report.Summary["Excused"] = records.Count(r => r.Status == AttendanceStatus.Excused).ToString();
        report.Summary["Attendance Rate"] = total == 0
            ? "0%"
            : $"{Math.Round(records.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late) * 100.0 / total, 1)}%";

        return report;
    }

    // ------------------------------------------------------------------
    // 3. Grades
    // ------------------------------------------------------------------
    private async Task<ReportResult> GradeReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var query = _db.GradeRecords
            .Include(g => g.Student).ThenInclude(s => s.GradeLevel)
            .Include(g => g.Student).ThenInclude(s => s.Section)
            .Include(g => g.Subject)
            .AsQueryable();

        if (f.SectionId is > 0) query = query.Where(g => g.SectionId == f.SectionId);
        if (f.SubjectId is > 0) query = query.Where(g => g.SubjectId == f.SubjectId);
        if (f.GradeLevelId is > 0) query = query.Where(g => g.Student!.GradeLevelId == f.GradeLevelId);
        if (f.Term is > 0) query = query.Where(g => g.Term == f.Term);
        if (!string.IsNullOrWhiteSpace(f.SchoolYear)) query = query.Where(g => g.SchoolYear == f.SchoolYear);

        var records = await query
            .OrderBy(g => g.Student!.LastName).ThenBy(g => g.Subject!.Code).ThenBy(g => g.Term)
            .ToListAsync();

        var report = new ReportResult
        {
            Title = "Academic Performance Report",
            Subtitle = $"{SchoolInfo.SchoolName} • S.Y. {f.SchoolYear}",
            FilterSummary = BuildFilterSummary(f, ("Section", await SectionLabelAsync(f.SectionId)), ("Subject", await SubjectLabelAsync(f.SubjectId)), ("Term", f.Term?.ToString() ?? "All")),
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "Student No." },
                new() { Header = "Student Name" },
                new() { Header = "Subject" },
                new() { Header = "Tri", Align = "center" },
                new() { Header = "Written Work", Align = "right" },
                new() { Header = "Performance Task", Align = "right" },
                new() { Header = "Term Exam", Align = "right" },
                new() { Header = "Final Grade", Align = "right" },
                new() { Header = "Remarks", Align = "center" }
            }
        };

        foreach (var g in records)
        {
            report.Rows.Add(new ReportRow(
                g.Student?.StudentNumber ?? "-",
                g.Student?.FullName ?? "-",
                g.Subject?.Code ?? "-",
                g.Term.ToString(),
                g.WrittenWork?.ToString("0.00") ?? "-",
                g.PerformanceTask?.ToString("0.00") ?? "-",
                g.TermAssessment?.ToString("0.00") ?? "-",
                g.FinalGrade?.ToString("0.00") ?? "-",
                g.FinalGrade.GradeRemarks()));
        }

        var graded = records.Where(r => r.FinalGrade.HasValue).ToList();
        report.Summary["Records"] = records.Count.ToString();
        report.Summary["Graded"] = graded.Count.ToString();
        report.Summary["Class Average"] = graded.Count == 0 ? "-" : graded.Average(r => r.FinalGrade!.Value).ToString("0.00");
        report.Summary["Passed"] = graded.Count(r => r.FinalGrade >= 75).ToString();
        report.Summary["Failed"] = graded.Count(r => r.FinalGrade < 75).ToString();
        report.Summary["Highest"] = graded.Count == 0 ? "-" : graded.Max(r => r.FinalGrade!.Value).ToString("0.00");
        report.Summary["Lowest"] = graded.Count == 0 ? "-" : graded.Min(r => r.FinalGrade!.Value).ToString("0.00");

        return report;
    }

    // ------------------------------------------------------------------
    // 4. Enrollment summary
    // ------------------------------------------------------------------
    private async Task<ReportResult> EnrollmentReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var students = await _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .ToListAsync();

        var report = new ReportResult
        {
            Title = "Enrollment Summary Report",
            Subtitle = $"{SchoolInfo.SchoolName} • School Year {f.SchoolYear}",
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "Grade Level" },
                new() { Header = "Section" },
                new() { Header = "Adviser" },
                new() { Header = "Male", Align = "right" },
                new() { Header = "Female", Align = "right" },
                new() { Header = "Total", Align = "right" },
                new() { Header = "Capacity", Align = "right" },
                new() { Header = "Utilisation", Align = "right" }
            }
        };

        var sections = await _db.Sections.Include(s => s.GradeLevel).Include(s => s.Adviser)
            .OrderBy(s => s.GradeLevel!.OrderIndex).ThenBy(s => s.Name).ToListAsync();

        foreach (var section in sections)
        {
            var members = students.Where(s => s.SectionId == section.Id).ToList();
            var male = members.Count(s => s.Gender == Gender.Male);
            var female = members.Count(s => s.Gender == Gender.Female);
            var utilisation = section.Capacity == 0 ? "0%" : $"{Math.Round(members.Count * 100.0 / section.Capacity)}%";

            report.Rows.Add(new ReportRow(
                section.GradeLevel?.Name ?? "-",
                section.Name,
                section.Adviser?.FullName ?? "Unassigned",
                male.ToString(),
                female.ToString(),
                members.Count.ToString(),
                section.Capacity.ToString(),
                utilisation));
        }

        var unassigned = students.Count(s => s.SectionId == null);
        if (unassigned > 0)
        {
            report.Rows.Add(new ReportRow("Unassigned", "-", "-", "-", "-", unassigned.ToString(), "-", "-"));
        }

        report.Summary["Total Students"] = students.Count.ToString();
        report.Summary["Total Sections"] = sections.Count.ToString();
        report.Summary["Male"] = students.Count(s => s.Gender == Gender.Male).ToString();
        report.Summary["Female"] = students.Count(s => s.Gender == Gender.Female).ToString();
        report.Summary["Average Section Size"] = sections.Count == 0 ? "-" : Math.Round(students.Count / (double)sections.Count, 1).ToString();
        report.Note = "Utilisation is computed as enrolled students divided by the declared section capacity.";

        return report;
    }

    // ------------------------------------------------------------------
    // 5. Document requests
    // ------------------------------------------------------------------
    private async Task<ReportResult> DocumentRequestReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var from = (f.DateFrom ?? DateTime.Today.AddDays(-90)).Date;
        var to = (f.DateTo ?? DateTime.Today).Date;

        var query = _db.DocumentRequests
            .Include(r => r.Student).ThenInclude(s => s.Section)
            .Include(r => r.DocumentType)
            .Include(r => r.ProcessedBy)
            .Where(r => r.RequestedAt >= from && r.RequestedAt < to.AddDays(1));

        if (!string.IsNullOrWhiteSpace(f.Status) && Enum.TryParse<DocumentRequestStatus>(f.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var requests = await query.OrderByDescending(r => r.RequestedAt).ToListAsync();

        var report = new ReportResult
        {
            Title = "Document Requests Report",
            Subtitle = $"{SchoolInfo.SchoolName} • {from:MMM d, yyyy} to {to:MMM d, yyyy}",
            FilterSummary = BuildFilterSummary(f, ("Status", f.Status ?? "All")),
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "Request No." },
                new() { Header = "Student" },
                new() { Header = "Document" },
                new() { Header = "Copies", Align = "center" },
                new() { Header = "Requested", Align = "center" },
                new() { Header = "Status", Align = "center" },
                new() { Header = "Processed By" }
            }
        };

        foreach (var r in requests)
        {
            report.Rows.Add(new ReportRow(
                r.RequestNumber,
                r.Student?.FullName ?? "-",
                r.DocumentType?.Name ?? "-",
                r.Copies.ToString(),
                r.RequestedAt.ToLocalTime().ToDateLabel(),
                r.Status.DisplayName(),
                r.ProcessedBy?.FullName ?? "Unprocessed"));
        }

        report.Summary["Total Requests"] = requests.Count.ToString();
        report.Summary["Pending"] = requests.Count(r => r.Status == DocumentRequestStatus.Pending).ToString();
        report.Summary["Processing"] = requests.Count(r => r.Status == DocumentRequestStatus.Processing).ToString();
        report.Summary["Ready for Release"] = requests.Count(r => r.Status == DocumentRequestStatus.ReadyForRelease).ToString();
        report.Summary["Released"] = requests.Count(r => r.Status == DocumentRequestStatus.Released).ToString();
        report.Summary["Rejected"] = requests.Count(r => r.Status == DocumentRequestStatus.Rejected).ToString();

        return report;
    }

    // ------------------------------------------------------------------
    // 6. Teacher schedule
    // ------------------------------------------------------------------
    private async Task<ReportResult> TeacherScheduleReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var query = _db.ClassSchedules
            .Include(c => c.Subject)
            .Include(c => c.Section).ThenInclude(s => s.GradeLevel)
            .Include(c => c.Teacher)
            .AsQueryable();

        if (f.TeacherId is > 0) query = query.Where(c => c.TeacherId == f.TeacherId);
        if (!string.IsNullOrWhiteSpace(f.SchoolYear)) query = query.Where(c => c.SchoolYear == f.SchoolYear);

        var schedules = (await query.ToListAsync())
            .OrderBy(c => c.Teacher!.LastName).ThenBy(c => c.DayOfWeek).ThenBy(c => c.StartTime)
            .ToList();

        var teacherLabel = f.TeacherId is > 0
            ? (await _db.Teachers.FindAsync(f.TeacherId))?.FullName ?? "Teacher"
            : "All Teachers";

        var report = new ReportResult
        {
            Title = "Teaching Load / Class Schedule",
            Subtitle = $"{SchoolInfo.SchoolName} • {teacherLabel} • S.Y. {f.SchoolYear}",
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "Teacher" },
                new() { Header = "Day", Align = "center" },
                new() { Header = "Time", Align = "center" },
                new() { Header = "Subject" },
                new() { Header = "Section" },
                new() { Header = "Room", Align = "center" }
            }
        };

        foreach (var c in schedules)
        {
            report.Rows.Add(new ReportRow(
                c.Teacher?.FullName ?? "-",
                c.DayOfWeek.DayShortName(),
                c.TimeRange,
                $"{c.Subject?.Code} - {c.Subject?.Title}",
                c.Section?.Name ?? "-",
                c.Room ?? "-"));
        }

        report.Summary["Schedule Entries"] = schedules.Count.ToString();
        report.Summary["Teachers Involved"] = schedules.Select(c => c.TeacherId).Distinct().Count().ToString();
        report.Summary["Weekly Hours"] = schedules.Sum(c => c.DurationHours).ToString("0.##");
        report.Summary["Conflicts Detected"] = (await CountConflictsAsync()).ToString();

        return report;
    }

    private async Task<int> CountConflictsAsync()
    {
        var schedules = await _db.ClassSchedules.ToListAsync();
        var conflicts = 0;

        foreach (var day in schedules.GroupBy(s => s.DayOfWeek))
        {
            var items = day.OrderBy(s => s.StartTime).ToList();
            for (var i = 0; i < items.Count; i++)
            {
                for (var j = i + 1; j < items.Count; j++)
                {
                    if (items[j].StartTime >= items[i].EndTime) break;
                    var overlap = items[j].StartTime < items[i].EndTime && items[i].StartTime < items[j].EndTime;
                    if (!overlap) continue;

                    var sameTeacher = items[i].TeacherId == items[j].TeacherId;
                    var sameSection = items[i].SectionId == items[j].SectionId;
                    var sameRoom = !string.IsNullOrWhiteSpace(items[i].Room) &&
                                   string.Equals(items[i].Room, items[j].Room, StringComparison.OrdinalIgnoreCase);

                    if (sameTeacher || sameSection || sameRoom) conflicts++;
                }
            }
        }

        return conflicts;
    }

    // ------------------------------------------------------------------
    // 7. Class list
    // ------------------------------------------------------------------
    private async Task<ReportResult> ClassListReportAsync(ReportFilterViewModel f, string generatedBy)
    {
        var section = f.SectionId is > 0
            ? await _db.Sections.Include(s => s.GradeLevel).Include(s => s.Adviser).FirstOrDefaultAsync(s => s.Id == f.SectionId)
            : await _db.Sections.Include(s => s.GradeLevel).Include(s => s.Adviser).FirstOrDefaultAsync();

        var students = section is null
            ? new List<Models.Entities.Student>()
            : await _db.Students
                .Include(s => s.Guardian)
                .Where(s => s.SectionId == section.Id)
                .OrderBy(s => s.Gender).ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToListAsync();

        var report = new ReportResult
        {
            Title = "Class List",
            Subtitle = section is null
                ? SchoolInfo.SchoolName
                : $"{section.GradeLevel?.Name} - {section.Name} • Adviser: {section.Adviser?.FullName ?? "Unassigned"}",
            FilterSummary = BuildFilterSummary(f, ("School Year", f.SchoolYear)),
            GeneratedBy = generatedBy,
            Columns = new List<ReportColumn>
            {
                new() { Header = "No." },
                new() { Header = "Student No." },
                new() { Header = "Name" },
                new() { Header = "Sex", Align = "center" },
                new() { Header = "Birth Date", Align = "center" },
                new() { Header = "Guardian" },
                new() { Header = "Guardian Contact" }
            }
        };

        var index = 1;
        foreach (var s in students)
        {
            report.Rows.Add(new ReportRow(
                (index++).ToString(),
                s.StudentNumber,
                s.FullName,
                s.Gender.DisplayName(),
                s.BirthDate?.ToDateLabel() ?? "-",
                s.Guardian?.FullName ?? "-",
                s.Guardian?.ContactNumber ?? "-"));
        }

        report.Summary["Section"] = section?.Name ?? "-";
        report.Summary["Total Students"] = students.Count.ToString();
        report.Summary["Male"] = students.Count(s => s.Gender == Gender.Male).ToString();
        report.Summary["Female"] = students.Count(s => s.Gender == Gender.Female).ToString();

        return report;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private async Task<string?> GradeLabelAsync(int? id) =>
        id is > 0 ? (await _db.GradeLevels.FindAsync(id))?.Name : null;

    private async Task<string?> SectionLabelAsync(int? id) =>
        id is > 0 ? (await _db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == id))?.DisplayName : null;

    private async Task<string?> SubjectLabelAsync(int? id) =>
        id is > 0 ? (await _db.Subjects.FindAsync(id))?.DisplayName : null;

    private static string BuildFilterSummary(ReportFilterViewModel f, params (string Label, string? Value)[] extras)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.SchoolYear)) parts.Add($"School Year: {f.SchoolYear}");
        if (!string.IsNullOrWhiteSpace(f.Keyword)) parts.Add($"Keyword: {f.Keyword}");

        foreach (var (label, value) in extras)
        {
            if (!string.IsNullOrWhiteSpace(value)) parts.Add($"{label}: {value}");
        }

        return parts.Count == 0 ? "Filters: none" : "Filters: " + string.Join(" | ", parts);
    }
}
