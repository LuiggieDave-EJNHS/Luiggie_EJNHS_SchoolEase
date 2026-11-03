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

/// <summary>Administration of class sections and their student / subject assignments.</summary>
[Authorize(Policy = Policies.AcademicOversight)]
public class SectionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SectionsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IActionResult> Index(int? gradeLevelId, string? keyword, int page = 1)
    {
        var query = _db.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Adviser)
            .AsQueryable();

        if (gradeLevelId is > 0)
        {
            query = query.Where(s => s.GradeLevelId == gradeLevelId);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.Code.Contains(term)
                || (s.TrackStrand != null && s.TrackStrand.Contains(term)));
        }

        query = query.OrderBy(s => s.GradeLevel!.OrderIndex).ThenBy(s => s.Name);

        var paged = await PagedList<Section>.CreateAsync(query, page, 15);

        var ids = paged.Select(s => s.Id).ToList();
        var counts = await _db.Students
            .Where(st => st.SectionId != null && ids.Contains(st.SectionId.Value))
            .GroupBy(st => st.SectionId!.Value)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        foreach (var section in paged)
        {
            section.StudentCount = counts.TryGetValue(section.Id, out var count) ? count : 0;
        }

        ViewData["Title"] = "Classes & Sections";
        ViewData["GradeLevelId"] = gradeLevelId;
        ViewData["Keyword"] = keyword;
        ViewData["GradeLevelOptions"] = await GradeLevelOptionsAsync(gradeLevelId);
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "Sections",
            RouteValues = new()
            {
                ["gradeLevelId"] = gradeLevelId?.ToString(),
                ["keyword"] = keyword
            }
        };

        return View(paged);
    }

    public async Task<IActionResult> Details(int id)
    {
        var section = await _db.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Adviser)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section is null) return NotFound();

        section.StudentCount = await _db.Students.CountAsync(s => s.SectionId == id);

        var students = await _db.Students
            .Where(s => s.SectionId == id)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var assignments = await _db.TeacherSubjectAssignments
            .Include(a => a.Teacher)
            .Include(a => a.Subject)
            .Where(a => a.SectionId == id)
            .OrderBy(a => a.Subject!.Code)
            .ToListAsync();

        var schedules = await _db.ClassSchedules
            .Include(c => c.Subject)
            .Include(c => c.Teacher)
            .Where(c => c.SectionId == id)
            .ToListAsync();
        schedules = schedules.OrderBy(c => c.DayOfWeek).ThenBy(c => c.StartTime).ToList();

        ViewData["Title"] = section.DisplayName;
        return View(new SectionDetailsViewModel
        {
            Section = section,
            Students = students,
            Assignments = assignments,
            Schedules = schedules
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new SectionFormViewModel { SchoolYear = SchoolInfo.SchoolYear };
        await PopulateFormListsAsync(model);
        ViewData["Title"] = "New Section";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SectionFormViewModel model)
    {
        await ValidateAsync(model, null);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "New Section";
            return View(model);
        }

        var section = new Section
        {
            Name = model.Name.Trim(),
            Code = model.Code.Trim(),
            GradeLevelId = model.GradeLevelId,
            AdviserId = model.AdviserId,
            TrackStrand = model.TrackStrand?.Trim(),
            Room = model.Room?.Trim(),
            Capacity = model.Capacity,
            SchoolYear = model.SchoolYear.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create Section", "Section", section.Id, section.Name);

        TempData["Success"] = $"Section \"{section.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null) return NotFound();

        var model = new SectionFormViewModel
        {
            Id = section.Id,
            Name = section.Name,
            Code = section.Code,
            GradeLevelId = section.GradeLevelId,
            AdviserId = section.AdviserId,
            TrackStrand = section.TrackStrand,
            Room = section.Room,
            Capacity = section.Capacity,
            SchoolYear = section.SchoolYear,
            IsActive = section.IsActive
        };

        await PopulateFormListsAsync(model);
        ViewData["Title"] = "Edit Section";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SectionFormViewModel model)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null) return NotFound();

        await ValidateAsync(model, id);

        if (!ModelState.IsValid)
        {
            await PopulateFormListsAsync(model);
            ViewData["Title"] = "Edit Section";
            return View(model);
        }

        section.Name = model.Name.Trim();
        section.Code = model.Code.Trim();
        section.GradeLevelId = model.GradeLevelId;
        section.AdviserId = model.AdviserId;
        section.TrackStrand = model.TrackStrand?.Trim();
        section.Room = model.Room?.Trim();
        section.Capacity = model.Capacity;
        section.SchoolYear = model.SchoolYear.Trim();
        section.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update Section", "Section", section.Id, section.Name);

        TempData["Success"] = $"Section \"{section.Name}\" updated.";
        return RedirectToAction(nameof(Details), new { id = section.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var section = await _db.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section is null)
        {
            TempData["Error"] = "Section not found.";
            return RedirectToAction(nameof(Index));
        }

        var studentCount = await _db.Students.CountAsync(s => s.SectionId == id);
        if (studentCount > 0)
        {
            TempData["Error"] = $"{studentCount} student(s) are still assigned to this section. Reassign them before deleting.";
            return RedirectToAction(nameof(Index));
        }

        _db.Sections.Remove(section);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Delete Section", "Section", section.Id, section.DisplayName);

        TempData["Success"] = $"Section \"{section.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    // -----------------------------------------------------------------
    // Assign students
    // -----------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> AssignStudents(int id)
    {
        var section = await _db.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section is null) return NotFound();

        var students = await _db.Students
            .Include(s => s.GradeLevel)
            .Where(s => s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        ViewData["Title"] = "Assign Students";
        return View(new AssignStudentsViewModel
        {
            Section = section,
            Students = students,
            SelectedIds = students.Where(s => s.SectionId == id).Select(s => s.Id).ToHashSet()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignStudents(int id, List<int>? studentIds)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null) return NotFound();

        var selected = (studentIds ?? new List<int>()).ToHashSet();

        var affected = await _db.Students
            .Where(s => s.SectionId == id || selected.Contains(s.Id))
            .ToListAsync();

        var changed = 0;
        foreach (var student in affected)
        {
            if (selected.Contains(student.Id))
            {
                if (student.SectionId != id || student.GradeLevelId != section.GradeLevelId)
                {
                    student.SectionId = id;
                    student.GradeLevelId = section.GradeLevelId;
                    student.UpdatedAt = DateTime.UtcNow;
                    changed++;
                }
            }
            else if (student.SectionId == id)
            {
                student.SectionId = null;
                student.UpdatedAt = DateTime.UtcNow;
                changed++;
            }
        }

        await _db.SaveChangesAsync();

        await UpsertEnrollmentsAsync(affected.Where(s => selected.Contains(s.Id)), section);

        await _audit.LogAsync("Assign Students", "Section", section.Id,
            $"{section.Name}: {changed} change(s)");

        TempData["Success"] = changed == 0
            ? "No student assignments changed."
            : $"{changed} student assignment(s) updated for {section.Name}.";

        return RedirectToAction(nameof(Details), new { id = section.Id });
    }

    // -----------------------------------------------------------------
    // Assign subjects
    // -----------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> AssignSubjects(int id)
    {
        var section = await _db.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section is null) return NotFound();

        var subjects = await _db.Subjects
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync();

        var assignments = await _db.TeacherSubjectAssignments
            .Where(a => a.SectionId == id)
            .ToListAsync();

        var items = subjects.Select(subject =>
        {
            var existing = assignments.FirstOrDefault(a => a.SubjectId == subject.Id);
            return new SectionSubjectItem
            {
                Subject = subject,
                Assigned = existing is not null,
                TeacherId = existing?.TeacherId
            };
        }).ToList();

        ViewData["Title"] = "Assign Subjects";
        return View(new AssignSubjectsViewModel
        {
            Section = section,
            Subjects = items,
            Teachers = await TeacherOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignSubjects(int id, List<int>? subjectIds, Dictionary<int, int>? teacherBySubject)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null) return NotFound();

        var selected = (subjectIds ?? new List<int>()).ToHashSet();
        teacherBySubject ??= new Dictionary<int, int>();

        var existing = await _db.TeacherSubjectAssignments
            .Where(a => a.SectionId == id && a.SchoolYear == section.SchoolYear)
            .ToListAsync();

        // Remove subjects that are no longer selected.
        foreach (var assignment in existing.Where(a => !selected.Contains(a.SubjectId)).ToList())
        {
            _db.TeacherSubjectAssignments.Remove(assignment);
        }

        // Create or update assignments for the checked subjects.
        foreach (var subjectId in selected)
        {
            if (!teacherBySubject.TryGetValue(subjectId, out var teacherId) || teacherId <= 0) continue;

            var assignment = existing.FirstOrDefault(a => a.SubjectId == subjectId);
            if (assignment is null)
            {
                _db.TeacherSubjectAssignments.Add(new TeacherSubjectAssignment
                {
                    SectionId = id,
                    SubjectId = subjectId,
                    TeacherId = teacherId,
                    SchoolYear = section.SchoolYear,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                assignment.TeacherId = teacherId;
            }
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("Assign Subjects", "Section", section.Id,
            $"{section.Name}: {selected.Count} subject(s) selected");

        TempData["Success"] = $"Subject assignments for {section.Name} saved.";
        return RedirectToAction(nameof(Details), new { id = section.Id });
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private async Task ValidateAsync(SectionFormViewModel model, int? excludeId)
    {
        if (model.GradeLevelId > 0 && !await _db.GradeLevels.AnyAsync(g => g.Id == model.GradeLevelId))
        {
            ModelState.AddModelError(nameof(model.GradeLevelId), "Please choose a valid grade level.");
        }

        if (model.AdviserId is > 0 && !await _db.Teachers.AnyAsync(t => t.Id == model.AdviserId))
        {
            ModelState.AddModelError(nameof(model.AdviserId), "Please choose a valid adviser.");
        }

        var code = model.Code?.Trim() ?? string.Empty;
        var schoolYear = model.SchoolYear?.Trim() ?? string.Empty;
        if (code.Length > 0 && schoolYear.Length > 0 &&
            await _db.Sections.AnyAsync(s => s.Code == code && s.SchoolYear == schoolYear && s.Id != (excludeId ?? 0)))
        {
            ModelState.AddModelError(nameof(model.Code), "A section with this code already exists for the selected school year.");
        }
    }

    private async Task PopulateFormListsAsync(SectionFormViewModel model)
    {
        model.GradeLevels = await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), model.GradeLevelId == g.Id))
            .ToListAsync();

        model.Advisers = await _db.Teachers
            .Where(t => t.IsActive)
            .OrderBy(t => t.LastName)
            .Select(t => new SelectListItem(t.FirstName + " " + t.LastName, t.Id.ToString(), model.AdviserId == t.Id))
            .ToListAsync();
    }

    private async Task UpsertEnrollmentsAsync(IEnumerable<Student> students, Section section)
    {
        var list = students.ToList();
        if (list.Count == 0) return;

        var ids = list.Select(s => s.Id).ToList();
        var existing = await _db.Enrollments
            .Where(e => ids.Contains(e.StudentId) && e.SchoolYear == section.SchoolYear)
            .ToListAsync();

        foreach (var student in list)
        {
            var enrollment = existing.FirstOrDefault(e => e.StudentId == student.Id);
            if (enrollment is null)
            {
                _db.Enrollments.Add(new Enrollment
                {
                    StudentId = student.Id,
                    GradeLevelId = section.GradeLevelId,
                    SectionId = section.Id,
                    SchoolYear = section.SchoolYear,
                    Status = student.EnrollmentStatus,
                    DateEnrolled = DateTime.Today,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                enrollment.GradeLevelId = section.GradeLevelId;
                enrollment.SectionId = section.Id;
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<List<SelectListItem>> GradeLevelOptionsAsync(int? selected = null) =>
        await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected.HasValue && selected.Value == g.Id))
            .ToListAsync();

    private async Task<List<SelectListItem>> TeacherOptionsAsync(int? selected = null) =>
        await _db.Teachers
            .Where(t => t.IsActive)
            .OrderBy(t => t.LastName)
            .Select(t => new SelectListItem(t.FirstName + " " + t.LastName, t.Id.ToString(), selected.HasValue && selected.Value == t.Id))
            .ToListAsync();
}
