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

/// <summary>Administration of the weekly class timetable.</summary>
[Authorize(Policy = Policies.AcademicOversight)]
public class SchedulesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SchedulesController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // -----------------------------------------------------------------
    // Index
    // -----------------------------------------------------------------

    public async Task<IActionResult> Index(int? sectionId, int? teacherId, DayOfWeek? day, int page = 1)
    {
        var query = ClassScheduleQuery(sectionId, teacherId, day);

        // Load the full filtered set to detect clashes (pairwise, in memory).
        var filtered = await query.ToListAsync();
        var (messages, conflictedIds) = DetectConflicts(filtered);

        // Page in memory: SQLite cannot ORDER BY TimeSpan server-side.
        const int pageSize = 15;
        if (page < 1) page = 1;
        var ordered = filtered.OrderBy(c => c.DayOfWeek).ThenBy(c => c.StartTime).ToList();
        var paged = new PagedList<ClassSchedule>(
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            ordered.Count,
            page,
            pageSize);

        ViewData["Title"] = "Schedules";
        ViewData["SectionId"] = sectionId;
        ViewData["TeacherId"] = teacherId;
        ViewData["Day"] = day;
        ViewData["Conflicts"] = messages;
        ViewData["ConflictedIds"] = conflictedIds;
        ViewData["SectionOptions"] = await SectionOptionsAsync(sectionId);
        ViewData["TeacherOptions"] = await TeacherOptionsAsync(teacherId);
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "Schedules",
            RouteValues = new()
            {
                ["sectionId"] = sectionId?.ToString(),
                ["teacherId"] = teacherId?.ToString(),
                ["day"] = day.HasValue ? ((int)day.Value).ToString() : null
            }
        };

        return View(paged);
    }

    // -----------------------------------------------------------------
    // Weekly grid
    // -----------------------------------------------------------------

    public async Task<IActionResult> Grid(int? sectionId, int? teacherId)
    {
        var schedules = await ClassScheduleQuery(sectionId, teacherId, null).ToListAsync();
        schedules = schedules.OrderBy(c => c.StartTime).ToList();

        var slots = schedules.Select(c => c.StartTime).Distinct().OrderBy(t => t).ToList();

        var sectionName = sectionId is > 0
            ? (await _db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == sectionId))?.DisplayName
            : null;
        var teacherName = teacherId is > 0
            ? (await _db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId))?.FullName
            : null;

        ViewData["Title"] = "Weekly Timetable";
        ViewData["SectionId"] = sectionId;
        ViewData["TeacherId"] = teacherId;
        ViewData["SectionOptions"] = await SectionOptionsAsync(sectionId);
        ViewData["TeacherOptions"] = await TeacherOptionsAsync(teacherId);

        return View(new ScheduleGridViewModel
        {
            Schedules = schedules,
            Slots = slots,
            SectionName = sectionName,
            TeacherName = teacherName
        });
    }

    // -----------------------------------------------------------------
    // Create / edit
    // -----------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create(int? sectionId, int? teacherId, int? subjectId)
    {
        var model = new ScheduleFormViewModel { SchoolYear = SchoolInfo.SchoolYear };
        if (sectionId is > 0) model.SectionId = sectionId.Value;
        if (teacherId is > 0) model.TeacherId = teacherId.Value;
        if (subjectId is > 0) model.SubjectId = subjectId.Value;

        await PopulateFormListsAsync(model);
        ViewData["Title"] = "New Schedule";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScheduleFormViewModel model)
    {
        await ValidateAsync(model, null);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "New Schedule";
            return View(model);
        }

        var schedule = new ClassSchedule
        {
            SectionId = model.SectionId,
            SubjectId = model.SubjectId,
            TeacherId = model.TeacherId,
            Room = model.Room?.Trim(),
            DayOfWeek = model.DayOfWeek,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            SchoolYear = model.SchoolYear.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.ClassSchedules.Add(schedule);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Schedule", "ClassSchedule", schedule.Id,
            $"{model.DayOfWeek} {schedule.TimeRange}");

        TempData["Success"] = "Class schedule created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(c => c.Id == id);
        if (schedule is null) return NotFound();

        var model = new ScheduleFormViewModel
        {
            Id = schedule.Id,
            SectionId = schedule.SectionId,
            SubjectId = schedule.SubjectId,
            TeacherId = schedule.TeacherId,
            Room = schedule.Room,
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            SchoolYear = schedule.SchoolYear
        };

        await PopulateFormListsAsync(model);
        ViewData["Title"] = "Edit Schedule";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScheduleFormViewModel model)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(c => c.Id == id);
        if (schedule is null) return NotFound();

        await ValidateAsync(model, id);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "Edit Schedule";
            return View(model);
        }

        schedule.SectionId = model.SectionId;
        schedule.SubjectId = model.SubjectId;
        schedule.TeacherId = model.TeacherId;
        schedule.Room = model.Room?.Trim();
        schedule.DayOfWeek = model.DayOfWeek;
        schedule.StartTime = model.StartTime;
        schedule.EndTime = model.EndTime;
        schedule.SchoolYear = model.SchoolYear.Trim();

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Schedule", "ClassSchedule", schedule.Id,
            $"{model.DayOfWeek} {schedule.TimeRange}");

        TempData["Success"] = "Class schedule updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(c => c.Id == id);
        if (schedule is null)
        {
            TempData["Error"] = "Schedule not found.";
            return RedirectToAction(nameof(Index));
        }

        _db.ClassSchedules.Remove(schedule);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete Schedule", "ClassSchedule", schedule.Id, schedule.TimeRange);

        TempData["Success"] = "Class schedule deleted.";
        return RedirectToAction(nameof(Index));
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private IQueryable<ClassSchedule> ClassScheduleQuery(int? sectionId, int? teacherId, DayOfWeek? day)
    {
        var query = _db.ClassSchedules
            .Include(c => c.Subject)
            .Include(c => c.Section).ThenInclude(s => s!.GradeLevel)
            .Include(c => c.Teacher)
            .AsQueryable();

        if (sectionId is > 0) query = query.Where(c => c.SectionId == sectionId);
        if (teacherId is > 0) query = query.Where(c => c.TeacherId == teacherId);
        if (day.HasValue) query = query.Where(c => c.DayOfWeek == day);

        return query;
    }

    private async Task ValidateAsync(ScheduleFormViewModel model, int? excludeId)
    {
        if (model.EndTime <= model.StartTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "End time must be later than the start time.");
        }

        if (model.SectionId <= 0 || !await _db.Sections.AnyAsync(s => s.Id == model.SectionId))
        {
            ModelState.AddModelError(nameof(model.SectionId), "Please choose a valid section.");
        }

        if (model.SubjectId <= 0 || !await _db.Subjects.AnyAsync(s => s.Id == model.SubjectId))
        {
            ModelState.AddModelError(nameof(model.SubjectId), "Please choose a valid subject.");
        }

        if (model.TeacherId <= 0 || !await _db.Teachers.AnyAsync(t => t.Id == model.TeacherId))
        {
            ModelState.AddModelError(nameof(model.TeacherId), "Please choose a valid teacher.");
        }

        if (!ModelState.IsValid) return;

        var conflict = await FindConflictAsync(model, excludeId);
        if (conflict is not null)
        {
            ModelState.AddModelError(string.Empty, conflict);
        }
    }

    /// <summary>Detects a clash on the same day for the same teacher, section or room.</summary>
    private async Task<string?> FindConflictAsync(ScheduleFormViewModel model, int? excludeId)
    {
        // Filter by day in SQL, then compare the time ranges in memory so the check
        // behaves identically on every supported database provider.
        var sameDay = await _db.ClassSchedules
            .Include(c => c.Teacher)
            .Include(c => c.Section)
            .Include(c => c.Subject)
            .Where(c => c.DayOfWeek == model.DayOfWeek && (excludeId == null || c.Id != excludeId))
            .ToListAsync();

        foreach (var existing in sameDay)
        {
            var overlaps = existing.StartTime < model.EndTime && model.StartTime < existing.EndTime;
            if (!overlaps) continue;

            if (existing.TeacherId == model.TeacherId)
            {
                return $"Teacher {existing.Teacher?.FullName} is already teaching in room {existing.Room ?? "—"} " +
                       $"from {existing.StartTime.To12Hour()} to {existing.EndTime.To12Hour()} on {model.DayOfWeek}.";
            }

            if (existing.SectionId == model.SectionId)
            {
                return $"Section {existing.Section?.Name} already has {existing.Subject?.Code} scheduled " +
                       $"from {existing.StartTime.To12Hour()} to {existing.EndTime.To12Hour()} on {model.DayOfWeek}.";
            }

            var sameRoom = !string.IsNullOrWhiteSpace(model.Room)
                && !string.IsNullOrWhiteSpace(existing.Room)
                && string.Equals(existing.Room, model.Room, StringComparison.OrdinalIgnoreCase);
            if (sameRoom)
            {
                return $"Room {existing.Room} is already in use by {existing.Subject?.Code} ({existing.Section?.Name}) " +
                       $"from {existing.StartTime.To12Hour()} to {existing.EndTime.To12Hour()} on {model.DayOfWeek}.";
            }
        }

        return null;
    }

    /// <summary>Pairwise clash detection over an already materialised set.</summary>
    private static (List<string> Messages, HashSet<int> ConflictedIds) DetectConflicts(IReadOnlyList<ClassSchedule> items)
    {
        var messages = new List<string>();
        var conflicted = new HashSet<int>();

        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var a = items[i];
                var b = items[j];

                if (a.DayOfWeek != b.DayOfWeek) continue;
                if (!(a.StartTime < b.EndTime && b.StartTime < a.EndTime)) continue;

                var reasons = new List<string>();
                if (a.TeacherId == b.TeacherId) reasons.Add($"teacher {a.Teacher?.FullName}");
                if (a.SectionId == b.SectionId) reasons.Add($"section {a.Section?.Name}");
                if (!string.IsNullOrWhiteSpace(a.Room) && !string.IsNullOrWhiteSpace(b.Room)
                    && string.Equals(a.Room, b.Room, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add($"room {a.Room}");
                }

                if (reasons.Count == 0) continue;

                var primary = reasons[0];
                var extra = reasons.Count > 1 ? " and " + string.Join(" and ", reasons.Skip(1)) : string.Empty;

                conflicted.Add(a.Id);
                conflicted.Add(b.Id);
                messages.Add($"{char.ToUpperInvariant(primary[0])}{primary[1..]}{extra} clash: {a.Subject?.Code} ({a.Section?.Name}) {a.TimeRange} " +
                             $"overlaps {b.Subject?.Code} ({b.Section?.Name}) {b.TimeRange} on {a.DayOfWeek}.");
            }
        }

        return (messages, conflicted);
    }

    private async Task PopulateFormListsAsync(ScheduleFormViewModel model)
    {
        var sections = await _db.Sections
            .Include(s => s.GradeLevel)
            .ToListAsync();
        model.Sections = sections
            .OrderBy(s => s.GradeLevel?.OrderIndex).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), model.SectionId == s.Id))
            .ToList();

        var subjects = await _db.Subjects.OrderBy(s => s.Code).ToListAsync();
        model.Subjects = subjects
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), model.SubjectId == s.Id))
            .ToList();

        var teachers = await _db.Teachers.Where(t => t.IsActive).ToListAsync();
        model.Teachers = teachers
            .OrderBy(t => t.LastName)
            .Select(t => new SelectListItem(t.FullName, t.Id.ToString(), model.TeacherId == t.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> SectionOptionsAsync(int? selected = null)
    {
        var sections = await _db.Sections.Include(s => s.GradeLevel).ToListAsync();
        return sections
            .OrderBy(s => s.GradeLevel?.OrderIndex).ThenBy(s => s.Name)
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), selected.HasValue && selected.Value == s.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> TeacherOptionsAsync(int? selected = null)
    {
        var teachers = await _db.Teachers.Where(t => t.IsActive).ToListAsync();
        return teachers
            .OrderBy(t => t.LastName)
            .Select(t => new SelectListItem(t.FullName, t.Id.ToString(), selected.HasValue && selected.Value == t.Id))
            .ToList();
    }
}
