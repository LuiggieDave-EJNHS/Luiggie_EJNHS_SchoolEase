using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Daily attendance register and the teacher marking sheet.</summary>
[Authorize(Policy = Policies.StaffOnly)]
public class AttendanceController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public AttendanceController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    // -----------------------------------------------------------------------
    // Register (administrator/staff view)
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Index(
        int? sectionId, int? gradeLevelId, DateTime? date, AttendanceStatus? status, int? studentId, int page = 1)
    {
        var query = BuildQuery(sectionId, gradeLevelId, date, status, studentId);

        // Summary counts over the whole filtered set (before paging).
        var counts = await query
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountOf(AttendanceStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        var present = CountOf(AttendanceStatus.Present);
        var late = CountOf(AttendanceStatus.Late);
        var absent = CountOf(AttendanceStatus.Absent);
        var excused = CountOf(AttendanceStatus.Excused);
        var total = present + late + absent + excused;

        var ordered = query
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.Student.LastName)
            .ThenBy(a => a.Student.FirstName);

        var records = await PagedList<AttendanceRecord>.CreateAsync(ordered, page, PageSize);

        var vm = new AttendanceIndexViewModel
        {
            Records = records,
            SectionId = sectionId,
            GradeLevelId = gradeLevelId,
            Date = date,
            Status = status,
            StudentId = studentId,
            TotalCount = total,
            PresentCount = present,
            LateCount = late,
            AbsentCount = absent,
            ExcusedCount = excused,
            AttendanceRate = total == 0 ? 0 : Math.Round((present + late) / (double)total * 100, 1),
            Sections = await SectionOptionsAsync(sectionId),
            GradeLevels = await GradeLevelOptionsAsync(gradeLevelId),
            Students = await StudentOptionsAsync(studentId),
            StatusDistribution = BuildStatusDistribution(present, late, absent, excused),
            DailyTrend = await BuildDailyTrendAsync(sectionId, gradeLevelId, studentId)
        };

        ViewData["Title"] = "Attendance";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = records.PageIndex,
            TotalPages = records.TotalPages,
            TotalCount = records.TotalCount,
            PageSize = records.PageSize,
            Action = nameof(Index),
            Controller = "Attendance",
            RouteValues = new Dictionary<string, string?>
            {
                ["sectionId"] = sectionId?.ToString(),
                ["gradeLevelId"] = gradeLevelId?.ToString(),
                ["date"] = date?.ToString("yyyy-MM-dd"),
                ["status"] = status.HasValue ? ((int)status.Value).ToString() : null,
                ["studentId"] = studentId?.ToString()
            }
        };

        return View(vm);
    }

    // -----------------------------------------------------------------------
    // Marking sheet
    // -----------------------------------------------------------------------
    [Authorize(Policy = Policies.TeacherOrAdmin)]
    public async Task<IActionResult> Mark(int sectionId, DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        var sections = await AllowedSectionsAsync();

        if (sectionId <= 0 && sections.Count > 0 && int.TryParse(sections[0].Value, out var firstSection))
        {
            sectionId = firstSection;
        }

        var section = sectionId > 0
            ? await _db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == sectionId)
            : null;

        // A teacher may only open the register of a section they advise or teach;
        // without this check the roster of any section could be read by URL.
        if (section is not null && !await CanMarkSectionAsync(section.Id))
        {
            return Forbid();
        }

        var vm = new AttendanceMarkViewModel
        {
            SectionId = section?.Id ?? 0,
            Date = day,
            SectionLabel = section?.DisplayName ?? "Select a section",
            Sections = sections
        };

        if (section is not null)
        {
            var students = await _db.Students
                .Where(s => s.SectionId == section.Id)
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToListAsync();

            var existing = await _db.AttendanceRecords
                .Where(a => a.SectionId == section.Id && a.Date >= day && a.Date < day.AddDays(1))
                .ToListAsync();

            vm.Rows = students.Select(s =>
            {
                var rec = existing.FirstOrDefault(a => a.StudentId == s.Id);
                return new AttendanceSheetRow
                {
                    StudentId = s.Id,
                    StudentNumber = s.StudentNumber,
                    StudentName = s.FullName,
                    Status = rec?.Status ?? AttendanceStatus.Present,
                    Remarks = rec?.Remarks
                };
            }).ToList();
        }

        ViewData["Title"] = "Mark Attendance";
        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = Policies.TeacherOrAdmin)]
    public async Task<IActionResult> Save(int sectionId, DateTime date, List<AttendanceRowInput> rows)
    {
        if (!await CanMarkSectionAsync(sectionId))
        {
            return Forbid();
        }

        var day = date.Date;

        // The posted rows are client data: only learners who are actually
        // enrolled in this section may be marked, and each one only once.
        var sectionStudentIds = await _db.Students
            .Where(s => s.SectionId == sectionId)
            .Select(s => s.Id)
            .ToListAsync();
        var enrolled = sectionStudentIds.ToHashSet();

        var existing = await _db.AttendanceRecords
            .Where(a => a.SectionId == sectionId && a.Date >= day && a.Date < day.AddDays(1))
            .ToListAsync();

        var count = 0;
        var absentIds = new List<int>();
        var handled = new HashSet<int>();

        foreach (var row in rows ?? new List<AttendanceRowInput>())
        {
            if (!enrolled.Contains(row.StudentId) || !handled.Add(row.StudentId)) continue;

            var record = existing.FirstOrDefault(a => a.StudentId == row.StudentId);
            if (record is null)
            {
                record = new AttendanceRecord
                {
                    StudentId = row.StudentId,
                    SectionId = sectionId,
                    Date = day
                };
                _db.AttendanceRecords.Add(record);
            }

            record.Status = row.Status;
            record.Remarks = row.Remarks;
            record.RecordedByUserId = _currentUser.UserId;
            record.RecordedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            count++;

            if (row.Status == AttendanceStatus.Absent) absentIds.Add(row.StudentId);
        }

        await _db.SaveChangesAsync();

        // One summary notification per save when learners were marked absent.
        if (absentIds.Count > 0)
        {
            var absentStudents = await _db.Students.Where(s => absentIds.Contains(s.Id)).ToListAsync();
            var names = absentStudents.Select(s => s.FullName).ToList();
            var message = names.Count == 1
                ? $"{names[0]} was marked absent on {day:MMM d}."
                : $"{names.Count} learners were marked absent on {day:MMM d}.";
            await _notify.NotifyAdministratorsAsync("Absent learner recorded", message, NotificationType.Warning);
        }

        await _audit.LogAsync("Marked attendance", "AttendanceRecord", sectionId,
            $"{count} record(s) for section {sectionId} on {day:yyyy-MM-dd}");

        TempData["Success"] = $"{count} attendance record(s) saved for {day:MMM d, yyyy}.";
        return RedirectToAction(nameof(Mark), new { sectionId, date = day.ToString("yyyy-MM-dd") });
    }

    // -----------------------------------------------------------------------
    // CSV export
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Export(int? sectionId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = _db.AttendanceRecords
            .Include(a => a.Student)
            .Include(a => a.Section)
            .AsQueryable();

        if (sectionId.HasValue) query = query.Where(a => a.SectionId == sectionId.Value);
        if (dateFrom.HasValue) query = query.Where(a => a.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue) query = query.Where(a => a.Date < dateTo.Value.Date.AddDays(1));

        var records = await query
            .OrderBy(a => a.Date)
            .ThenBy(a => a.Section.Name)
            .ThenBy(a => a.Student.LastName)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Date,Student No.,Student,Section,Status,Remarks");
        foreach (var r in records)
        {
            csv.AppendLine(string.Join(',',
                Csv(r.Date.ToString("yyyy-MM-dd")),
                Csv(r.Student.StudentNumber),
                Csv(r.Student.FullName),
                Csv(r.Section.Name),
                Csv(r.Status.DisplayName()),
                Csv(r.Remarks)));
        }

        var exportSectionLabel = sectionId?.ToString() ?? "all";
        await _audit.LogAsync("Exported attendance CSV", "AttendanceRecord", null,
            $"{records.Count} record(s), section={exportSectionLabel}");

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"attendance_{DateTime.Now:yyyyMMdd}.csv");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private IQueryable<AttendanceRecord> BuildQuery(
        int? sectionId, int? gradeLevelId, DateTime? date, AttendanceStatus? status, int? studentId)
    {
        var query = _db.AttendanceRecords
            .Include(a => a.Student)
            .Include(a => a.Section).ThenInclude(s => s.GradeLevel)
            .Include(a => a.RecordedBy)
            .AsQueryable();

        if (sectionId.HasValue) query = query.Where(a => a.SectionId == sectionId.Value);
        if (gradeLevelId.HasValue) query = query.Where(a => a.Section.GradeLevelId == gradeLevelId.Value);
        if (date.HasValue)
        {
            var d0 = date.Value.Date;
            query = query.Where(a => a.Date >= d0 && a.Date < d0.AddDays(1));
        }
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (studentId.HasValue) query = query.Where(a => a.StudentId == studentId.Value);

        return query;
    }

    private static ChartData BuildStatusDistribution(int present, int late, int absent, int excused)
    {
        var data = new ChartData
        {
            Title = "Attendance Status Distribution"
        };

        void Add(string label, int value, string color)
        {
            if (value <= 0) return;
            data.Labels.Add(label);
            data.Values.Add(value);
            data.Colors.Add(color);
        }

        Add(AttendanceStatus.Present.DisplayName(), present, "#12a37a");
        Add(AttendanceStatus.Late.DisplayName(), late, "#e8930c");
        Add(AttendanceStatus.Absent.DisplayName(), absent, "#dc3545");
        Add(AttendanceStatus.Excused.DisplayName(), excused, "#0ea5c6");

        return data;
    }

    private async Task<ChartData> BuildDailyTrendAsync(int? sectionId, int? gradeLevelId, int? studentId)
    {
        var end = DateTime.Today.AddDays(1);
        var start = end.AddDays(-14);

        var trendQuery = _db.AttendanceRecords.AsQueryable();
        if (sectionId.HasValue) trendQuery = trendQuery.Where(a => a.SectionId == sectionId.Value);
        if (gradeLevelId.HasValue) trendQuery = trendQuery.Where(a => a.Section.GradeLevelId == gradeLevelId.Value);
        if (studentId.HasValue) trendQuery = trendQuery.Where(a => a.StudentId == studentId.Value);

        var dates = await trendQuery
            .Where(a => a.Date >= start && a.Date < end
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .Select(a => a.Date)
            .ToListAsync();

        var byDay = dates.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());

        var data = new ChartData { Title = "Daily Attendance Trend (Last 14 Days)" };
        for (var i = 13; i >= 0; i--)
        {
            var day = DateTime.Today.AddDays(-i);
            data.Labels.Add(day.ToString("MMM d"));
            data.Values.Add(byDay.TryGetValue(day.Date, out var value) ? value : 0);
        }

        return data;
    }

    private async Task<List<SelectListItem>> AllowedSectionsAsync()
    {
        var query = _db.Sections.Include(s => s.GradeLevel).Where(s => s.IsActive);

        if (!_currentUser.HasSchoolWideAccess)
        {
            var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);
            if (teacher is null)
            {
                return new List<SelectListItem>();
            }

            var assigned = await _db.TeacherSubjectAssignments
                .Where(a => a.TeacherId == teacher.Id)
                .Select(a => a.SectionId)
                .ToListAsync();

            var advised = await _db.Sections
                .Where(s => s.AdviserId == teacher.Id)
                .Select(s => s.Id)
                .ToListAsync();

            var allowed = assigned.Union(advised).Distinct().ToList();
            query = query.Where(s => allowed.Contains(s.Id));
        }

        var sections = await query
            .OrderBy(s => s.GradeLevel.OrderIndex).ThenBy(s => s.Name)
            .ToListAsync();

        return sections
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString()))
            .ToList();
    }

    private async Task<bool> CanMarkSectionAsync(int sectionId)
    {
        if (_currentUser.HasSchoolWideAccess) return true;
        if (!_currentUser.IsTeacher) return false;

        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);
        if (teacher is null) return false;

        if (await _db.Sections.AnyAsync(s => s.Id == sectionId && s.AdviserId == teacher.Id)) return true;

        return await _db.TeacherSubjectAssignments
            .AnyAsync(a => a.SectionId == sectionId && a.TeacherId == teacher.Id);
    }

    private async Task<List<SelectListItem>> SectionOptionsAsync(int? selected = null)
    {
        var sections = await _db.Sections
            .Include(s => s.GradeLevel)
            .OrderBy(s => s.GradeLevel.OrderIndex).ThenBy(s => s.Name)
            .ToListAsync();

        return sections
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), selected == s.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> GradeLevelOptionsAsync(int? selected)
    {
        var levels = await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync();
        return levels
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected == g.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> StudentOptionsAsync(int? selected)
    {
        var students = await _db.Students
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        return students
            .Select(s => new SelectListItem($"{s.StudentNumber} — {s.FullName}", s.Id.ToString(), selected == s.Id))
            .ToList();
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
