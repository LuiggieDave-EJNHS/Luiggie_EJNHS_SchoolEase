using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Administration of the school's grade levels.</summary>
[Authorize(Policy = Policies.AdminOnly)]
public class GradeLevelsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public GradeLevelsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var sectionCounts = await _db.Sections
            .GroupBy(s => s.GradeLevelId)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var subjectCounts = await _db.Subjects
            .Where(s => s.GradeLevelId != null)
            .GroupBy(s => s.GradeLevelId!.Value)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var studentCounts = await _db.Students
            .Where(s => s.GradeLevelId != null)
            .GroupBy(s => s.GradeLevelId!.Value)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var levels = await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .ThenBy(g => g.Name)
            .ToListAsync();

        var rows = levels.Select(g => new GradeLevelRowViewModel
        {
            GradeLevel = g,
            SectionCount = sectionCounts.TryGetValue(g.Id, out var sc) ? sc : 0,
            SubjectCount = subjectCounts.TryGetValue(g.Id, out var su) ? su : 0,
            StudentCount = studentCounts.TryGetValue(g.Id, out var st) ? st : 0
        }).ToList();

        ViewData["Title"] = "Grade Levels";
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Grade Level";
        return View(new GradeLevelFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GradeLevelFormViewModel model)
    {
        if (await _db.GradeLevels.AnyAsync(g => g.Code == model.Code))
        {
            ModelState.AddModelError(nameof(model.Code), "A grade level with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Grade Level";
            return View(model);
        }

        var level = new GradeLevel
        {
            Name = model.Name.Trim(),
            Code = model.Code.Trim(),
            OrderIndex = model.OrderIndex,
            Description = model.Description?.Trim(),
            IsActive = model.IsActive
        };

        _db.GradeLevels.Add(level);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Grade Level", "GradeLevel", level.Id, level.Name);

        TempData["Success"] = $"Grade level \"{level.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var level = await _db.GradeLevels.FirstOrDefaultAsync(g => g.Id == id);
        if (level is null) return NotFound();

        ViewData["Title"] = "Edit Grade Level";
        return View(new GradeLevelFormViewModel
        {
            Id = level.Id,
            Name = level.Name,
            Code = level.Code,
            OrderIndex = level.OrderIndex,
            Description = level.Description,
            IsActive = level.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, GradeLevelFormViewModel model)
    {
        var level = await _db.GradeLevels.FirstOrDefaultAsync(g => g.Id == id);
        if (level is null) return NotFound();

        if (await _db.GradeLevels.AnyAsync(g => g.Code == model.Code && g.Id != id))
        {
            ModelState.AddModelError(nameof(model.Code), "A grade level with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Grade Level";
            return View(model);
        }

        level.Name = model.Name.Trim();
        level.Code = model.Code.Trim();
        level.OrderIndex = model.OrderIndex;
        level.Description = model.Description?.Trim();
        level.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Grade Level", "GradeLevel", level.Id, level.Name);

        TempData["Success"] = $"Grade level \"{level.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var level = await _db.GradeLevels.FirstOrDefaultAsync(g => g.Id == id);
        if (level is null)
        {
            TempData["Error"] = "Grade level not found.";
            return RedirectToAction(nameof(Index));
        }

        var hasSections = await _db.Sections.AnyAsync(s => s.GradeLevelId == id);
        var hasSubjects = await _db.Subjects.AnyAsync(s => s.GradeLevelId == id);
        var hasStudents = await _db.Students.AnyAsync(s => s.GradeLevelId == id);

        if (hasSections || hasSubjects || hasStudents)
        {
            TempData["Error"] = "This grade level cannot be deleted because it still has sections, subjects or students.";
            return RedirectToAction(nameof(Index));
        }

        _db.GradeLevels.Remove(level);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete Grade Level", "GradeLevel", level.Id, level.Name);

        TempData["Success"] = $"Grade level \"{level.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }
}
