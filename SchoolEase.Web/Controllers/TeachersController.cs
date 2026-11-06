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

/// <summary>Teaching personnel register: profiles, advisory assignments and timetables.</summary>
[Authorize(Policy = Policies.AcademicOversight)]
public class TeachersController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public TeachersController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ----------------------------------------------------------------- Index
    public async Task<IActionResult> Index(string? keyword, string? department, EmploymentStatus? status, string? sort, int page = 1)
    {
        var query = _db.Teachers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(t =>
                t.FirstName.Contains(term) ||
                t.LastName.Contains(term) ||
                t.EmployeeNumber.Contains(term) ||
                (t.Email != null && t.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(t => t.Department == department);
        if (status is not null) query = query.Where(t => t.EmploymentStatus == status);

        query = sort switch
        {
            "number" => query.OrderBy(t => t.EmployeeNumber),
            "-number" => query.OrderByDescending(t => t.EmployeeNumber),
            "-name" => query.OrderByDescending(t => t.LastName).ThenByDescending(t => t.FirstName),
            "department" => query.OrderBy(t => t.Department).ThenBy(t => t.LastName),
            _ => query.OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
        };

        query = query.Include(t => t.AdvisorySections);

        var teachers = await PagedList<Teacher>.CreateAsync(query, page, PageSize);

        var vm = new TeacherIndexViewModel
        {
            Teachers = teachers,
            Keyword = keyword,
            Department = department,
            Status = status,
            Sort = sort,
            Departments = await BuildDepartmentOptionsAsync(department),
            Statuses = SelectLists.EnumOptions<EmploymentStatus>(status),
            Stats = new List<StatCard>
            {
                new() { Label = "Total Teachers", Value = (await _db.Teachers.CountAsync()).ToString("N0"), Icon = "bi-person-video3", Tone = "primary", Meta = $"{await _db.Teachers.CountAsync(t => t.IsActive)} active" },
                new() { Label = "Permanent", Value = (await _db.Teachers.CountAsync(t => t.EmploymentStatus == EmploymentStatus.Permanent)).ToString("N0"), Icon = "bi-patch-check", Tone = "success" },
                new() { Label = "Probationary", Value = (await _db.Teachers.CountAsync(t => t.EmploymentStatus == EmploymentStatus.Probationary)).ToString("N0"), Icon = "bi-hourglass-split", Tone = "info" },
                new() { Label = "Advisory Sections", Value = (await _db.Sections.CountAsync(s => s.AdviserId != null)).ToString("N0"), Icon = "bi-diagram-3", Tone = "teal", Meta = "Assigned class advisers" }
            }
        };

        ViewData["Title"] = "Teachers";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = teachers.PageIndex,
            TotalPages = teachers.TotalPages,
            TotalCount = teachers.TotalCount,
            PageSize = teachers.PageSize,
            Action = "Index",
            Controller = "Teachers",
            NoResultsText = "No teachers match your filters.",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["department"] = department,
                ["status"] = status?.ToString(),
                ["sort"] = sort
            }
        };

        return View(vm);
    }

    // --------------------------------------------------------------- Details
    public async Task<IActionResult> Details(int id)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();

        var schedule = (await _db.ClassSchedules
            .Where(c => c.TeacherId == id)
            .Include(c => c.Subject)
            .Include(c => c.Section)
            .ToListAsync())
            .OrderBy(c => c.DayOfWeek).ThenBy(c => c.StartTime)
            .ToList();

        var vm = new TeacherDetailsViewModel
        {
            Teacher = teacher,
            AdvisorySections = await _db.Sections
                .Where(s => s.AdviserId == id)
                .Include(s => s.GradeLevel)
                .OrderBy(s => s.Name)
                .ToListAsync(),
            SubjectAssignments = await _db.TeacherSubjectAssignments
                .Where(a => a.TeacherId == id)
                .Include(a => a.Subject)
                .Include(a => a.Section)
                .OrderBy(a => a.SchoolYear)
                .ToListAsync(),
            Schedule = schedule,
            WeeklyHours = schedule.Sum(c => c.DurationHours)
        };

        ViewData["Title"] = teacher.FullName;
        return View(vm);
    }

    // ---------------------------------------------------------------- Create
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Teacher";
        return View(new Teacher
        {
            DateHired = DateTime.Today,
            EmploymentStatus = EmploymentStatus.Permanent,
            IsActive = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Teacher model)
    {
        if (string.IsNullOrWhiteSpace(model.EmployeeNumber))
        {
            model.EmployeeNumber = await GenerateEmployeeNumberAsync();
        }
        else if (await _db.Teachers.AnyAsync(t => t.EmployeeNumber == model.EmployeeNumber))
        {
            ModelState.AddModelError(nameof(Teacher.EmployeeNumber), $"Employee number {model.EmployeeNumber} is already in use.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Teacher";
            return View(model);
        }

        model.IsActive = true;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;
        if (model.DateHired == default(DateTime)) model.DateHired = DateTime.Today;

        _db.Teachers.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Teacher", "Teacher", model.Id, model.FullName);
        TempData["Success"] = $"Teacher {model.FullName} was added.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ------------------------------------------------------------------ Edit
    public async Task<IActionResult> Edit(int id)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();

        ViewData["Title"] = $"Edit {teacher.FullName}";
        return View(teacher);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Teacher model)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.EmployeeNumber)) model.EmployeeNumber = teacher.EmployeeNumber;

        if (await _db.Teachers.AnyAsync(t => t.EmployeeNumber == model.EmployeeNumber && t.Id != id))
        {
            ModelState.AddModelError(nameof(Teacher.EmployeeNumber), $"Employee number {model.EmployeeNumber} is already in use.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            ViewData["Title"] = $"Edit {teacher.FullName}";
            return View(model);
        }

        teacher.EmployeeNumber = model.EmployeeNumber;
        teacher.FirstName = model.FirstName;
        teacher.MiddleName = model.MiddleName;
        teacher.LastName = model.LastName;
        teacher.Gender = model.Gender;
        teacher.BirthDate = model.BirthDate;
        teacher.Address = model.Address;
        teacher.ContactNumber = model.ContactNumber;
        teacher.Email = model.Email;
        teacher.Department = model.Department;
        teacher.Specialization = model.Specialization;
        teacher.EmploymentStatus = model.EmploymentStatus;
        teacher.DateHired = model.DateHired ?? teacher.DateHired;
        teacher.IsActive = model.IsActive;
        teacher.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Teacher", "Teacher", teacher.Id, teacher.FullName);
        TempData["Success"] = $"Teacher {teacher.FullName} was updated.";
        return RedirectToAction(nameof(Details), new { id = teacher.Id });
    }

    // ---------------------------------------------------------------- Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null) return NotFound();

        var name = teacher.FullName;

        // Class schedules keep a hard reference to their teacher, so the row
        // cannot be removed while any remain.
        if (await _db.ClassSchedules.AnyAsync(c => c.TeacherId == id))
        {
            TempData["Error"] = $"{name} still has classes in the timetable. Reassign or remove those schedules before deleting.";
            return RedirectToAction(nameof(Index));
        }

        _db.Teachers.Remove(teacher);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Teacher", "Teacher", id, name);
        TempData["Success"] = $"Teacher {name} was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Helpers
    private async Task<List<SelectListItem>> BuildDepartmentOptionsAsync(string? selected)
    {
        var departments = await _db.Teachers
            .Where(t => t.Department != null && t.Department != "")
            .Select(t => t.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        return departments.Select(d => new SelectListItem(d, d, selected == d)).ToList();
    }

    private async Task<string> GenerateEmployeeNumberAsync()
    {
        const string prefix = "T-";
        var existing = await _db.Teachers
            .Where(t => t.EmployeeNumber.StartsWith(prefix))
            .Select(t => t.EmployeeNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in existing)
        {
            if (int.TryParse(number[prefix.Length..], out var value) && value > max) max = value;
        }

        return $"{prefix}{max + 1:D3}";
    }
}
