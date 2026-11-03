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

/// <summary>Administration of the school's subject catalogue.</summary>
[Authorize(Policy = Policies.AcademicOversight)]
public class SubjectsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SubjectsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(string? keyword, int? gradeLevelId, int page = 1)
    {
        var query = _db.Subjects.Include(s => s.GradeLevel).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(s => s.Code.Contains(term) || s.Title.Contains(term));
        }

        if (gradeLevelId is > 0)
        {
            query = query.Where(s => s.GradeLevelId == gradeLevelId);
        }

        query = query.OrderBy(s => s.Code);

        var paged = await PagedList<Subject>.CreateAsync(query, page, 15);

        var ids = paged.Select(s => s.Id).ToList();
        var assignmentRows = await _db.TeacherSubjectAssignments
            .Where(a => ids.Contains(a.SubjectId))
            .Select(a => new { a.SubjectId, TeacherName = a.Teacher.FirstName + " " + a.Teacher.LastName })
            .ToListAsync();

        var rows = paged.Select(subject => new SubjectRowViewModel
        {
            Subject = subject,
            TeacherNames = assignmentRows
                .Where(a => a.SubjectId == subject.Id)
                .Select(a => a.TeacherName)
                .Distinct()
                .OrderBy(n => n)
                .ToList()
        }).ToList();

        foreach (var row in rows)
        {
            row.TeacherCount = row.TeacherNames.Count;
        }

        ViewData["Title"] = "Subjects";
        ViewData["Keyword"] = keyword;
        ViewData["GradeLevelId"] = gradeLevelId;
        ViewData["GradeLevelOptions"] = await GradeLevelOptionsAsync(gradeLevelId);
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "Subjects",
            RouteValues = new()
            {
                ["keyword"] = keyword,
                ["gradeLevelId"] = gradeLevelId?.ToString()
            }
        };

        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var subject = await _db.Subjects
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (subject is null) return NotFound();

        var assignments = await _db.TeacherSubjectAssignments
            .Include(a => a.Teacher)
            .Include(a => a.Section)
            .Where(a => a.SubjectId == id)
            .ToListAsync();

        var schedules = await _db.ClassSchedules
            .Include(c => c.Section)
            .Include(c => c.Teacher)
            .Where(c => c.SubjectId == id)
            .ToListAsync();
        schedules = schedules.OrderBy(c => c.DayOfWeek).ThenBy(c => c.StartTime).ToList();

        ViewData["Title"] = subject.DisplayName;
        return View(new SubjectDetailsViewModel
        {
            Subject = subject,
            Assignments = assignments,
            Schedules = schedules
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new SubjectFormViewModel();
        await PopulateFormListsAsync(model);
        ViewData["Title"] = "New Subject";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectFormViewModel model)
    {
        await ValidateAsync(model, null);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "New Subject";
            return View(model);
        }

        var subject = new Subject
        {
            Code = model.Code.Trim(),
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            Units = model.Units,
            GradeLevelId = model.GradeLevelId,
            TrackStrand = model.TrackStrand?.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Subject", "Subject", subject.Id, subject.DisplayName);

        TempData["Success"] = $"Subject \"{subject.Code}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        if (subject is null) return NotFound();

        var model = new SubjectFormViewModel
        {
            Id = subject.Id,
            Code = subject.Code,
            Title = subject.Title,
            Description = subject.Description,
            Units = subject.Units,
            GradeLevelId = subject.GradeLevelId,
            TrackStrand = subject.TrackStrand,
            IsActive = subject.IsActive
        };

        await PopulateFormListsAsync(model);
        ViewData["Title"] = "Edit Subject";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SubjectFormViewModel model)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        if (subject is null) return NotFound();

        await ValidateAsync(model, id);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "Edit Subject";
            return View(model);
        }

        subject.Code = model.Code.Trim();
        subject.Title = model.Title.Trim();
        subject.Description = model.Description?.Trim();
        subject.Units = model.Units;
        subject.GradeLevelId = model.GradeLevelId;
        subject.TrackStrand = model.TrackStrand?.Trim();
        subject.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Subject", "Subject", subject.Id, subject.DisplayName);

        TempData["Success"] = $"Subject \"{subject.Code}\" updated.";
        return RedirectToAction(nameof(Details), new { id = subject.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id);
        if (subject is null)
        {
            TempData["Error"] = "Subject not found.";
            return RedirectToAction(nameof(Index));
        }

        var hasAssignments = await _db.TeacherSubjectAssignments.AnyAsync(a => a.SubjectId == id);
        var hasSchedules = await _db.ClassSchedules.AnyAsync(c => c.SubjectId == id);

        if (hasAssignments || hasSchedules)
        {
            TempData["Error"] = "This subject cannot be deleted because it is still assigned to sections or scheduled in the timetable.";
            return RedirectToAction(nameof(Index));
        }

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete Subject", "Subject", subject.Id, subject.DisplayName);

        TempData["Success"] = $"Subject \"{subject.Code}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(SubjectFormViewModel model, int? excludeId)
    {
        if (model.GradeLevelId is > 0 && !await _db.GradeLevels.AnyAsync(g => g.Id == model.GradeLevelId))
        {
            ModelState.AddModelError(nameof(model.GradeLevelId), "Please choose a valid grade level.");
        }

        var code = model.Code?.Trim() ?? string.Empty;
        if (code.Length > 0 && await _db.Subjects.AnyAsync(s => s.Code == code && s.Id != (excludeId ?? 0)))
        {
            ModelState.AddModelError(nameof(model.Code), "A subject with this code already exists.");
        }
    }

    private async Task PopulateFormListsAsync(SubjectFormViewModel model)
    {
        model.GradeLevels = await GradeLevelOptionsAsync(model.GradeLevelId);
    }

    private async Task<List<SelectListItem>> GradeLevelOptionsAsync(int? selected = null) =>
        await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected.HasValue && selected.Value == g.Id))
            .ToListAsync();
}
