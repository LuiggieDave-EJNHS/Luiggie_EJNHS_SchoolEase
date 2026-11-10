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

/// <summary>Learner register: browsing, profiling and encoding of student records.</summary>
[Authorize(Policy = Policies.StaffOnly)]
public class StudentsController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public StudentsController(ApplicationDbContext db, IAuditService audit, INotificationService notify)
    {
        _db = db;
        _audit = audit;
        _notify = notify;
    }

    // ----------------------------------------------------------------- Index
    public async Task<IActionResult> Index(string? keyword, int? gradeLevelId, int? sectionId, EnrollmentStatus? status, string? sort, int page = 1)
    {
        var query = _db.Students.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(s =>
                s.FirstName.Contains(term) ||
                s.LastName.Contains(term) ||
                s.StudentNumber.Contains(term) ||
                (s.Lrn != null && s.Lrn.Contains(term)) ||
                (s.Email != null && s.Email.Contains(term)));
        }

        if (gradeLevelId is > 0) query = query.Where(s => s.GradeLevelId == gradeLevelId);
        if (sectionId is > 0) query = query.Where(s => s.SectionId == sectionId);
        if (status is not null) query = query.Where(s => s.EnrollmentStatus == status);

        query = sort switch
        {
            "name" => query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName),
            "-name" => query.OrderByDescending(s => s.LastName).ThenByDescending(s => s.FirstName),
            "number" => query.OrderBy(s => s.StudentNumber),
            "-number" => query.OrderByDescending(s => s.StudentNumber),
            "date" => query.OrderBy(s => s.DateEnrolled),
            _ => query.OrderByDescending(s => s.DateEnrolled)
        };

        query = query.Include(s => s.GradeLevel).Include(s => s.Section).Include(s => s.Guardian);

        var students = await PagedList<Student>.CreateAsync(query, page, PageSize);

        var maleCount = await _db.Students.CountAsync(s => s.Gender == Gender.Male);
        var femaleCount = await _db.Students.CountAsync(s => s.Gender == Gender.Female);

        var vm = new StudentIndexViewModel
        {
            Students = students,
            Keyword = keyword,
            GradeLevelId = gradeLevelId,
            SectionId = sectionId,
            Status = status,
            Sort = sort,
            GradeLevels = await BuildGradeLevelOptionsAsync(gradeLevelId),
            Sections = await BuildSectionOptionsAsync(sectionId),
            Statuses = SelectLists.EnumOptions<EnrollmentStatus>(status),
            Stats = new List<StatCard>
            {
                new() { Label = "Total Students", Value = (await _db.Students.CountAsync()).ToString("N0"), Icon = "bi-people", Tone = "primary", Meta = $"{await _db.Students.CountAsync(s => s.IsActive)} active" },
                new() { Label = "Enrolled", Value = (await _db.Students.CountAsync(s => s.EnrollmentStatus == EnrollmentStatus.Enrolled)).ToString("N0"), Icon = "bi-person-check", Tone = "success" },
                new() { Label = "Pending", Value = (await _db.Students.CountAsync(s => s.EnrollmentStatus == EnrollmentStatus.Pending)).ToString("N0"), Icon = "bi-hourglass-split", Tone = "warning" },
                new() { Label = "Male / Female", Value = $"{maleCount:N0} / {femaleCount:N0}", Icon = "bi-gender-ambiguous", Tone = "info", Meta = "By sex" }
            }
        };

        ViewData["Title"] = "Students";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = students.PageIndex,
            TotalPages = students.TotalPages,
            TotalCount = students.TotalCount,
            PageSize = students.PageSize,
            Action = "Index",
            Controller = "Students",
            NoResultsText = "No students match your filters.",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["gradeLevelId"] = gradeLevelId?.ToString(),
                ["sectionId"] = sectionId?.ToString(),
                ["status"] = status?.ToString(),
                ["sort"] = sort
            }
        };

        return View(vm);
    }

    // --------------------------------------------------------------- Details
    public async Task<IActionResult> Details(int id)
    {
        var student = await _db.Students
            .Include(s => s.Guardian)
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null) return NotFound();

        var statusCounts = await _db.AttendanceRecords
            .Where(a => a.StudentId == id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var attendance = new AttendanceSummary
        {
            Present = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Present)?.Count ?? 0,
            Late = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Late)?.Count ?? 0,
            Absent = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Absent)?.Count ?? 0,
            Excused = statusCounts.FirstOrDefault(x => x.Status == AttendanceStatus.Excused)?.Count ?? 0
        };

        var vm = new StudentDetailsViewModel
        {
            Student = student,
            Attendance = attendance,
            Grades = await _db.GradeRecords
                .Where(g => g.StudentId == id)
                .Include(g => g.Subject)
                .OrderBy(g => g.SchoolYear).ThenBy(g => g.Term)
                .ToListAsync(),
            DocumentRequests = await _db.DocumentRequests
                .Where(r => r.StudentId == id)
                .Include(r => r.DocumentType)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync(),
            RecentAttendance = await _db.AttendanceRecords
                .Where(a => a.StudentId == id)
                .Include(a => a.Subject)
                .OrderByDescending(a => a.Date)
                .Take(12)
                .ToListAsync()
        };

        ViewData["Title"] = student.FullName;
        return View(vm);
    }

    // ---------------------------------------------------------------- Create
    public async Task<IActionResult> Create()
    {
        await PopulateListsAsync();
        ViewData["Title"] = "Add Student";
        return View(new Student
        {
            StudentNumber = await GenerateStudentNumberAsync(),
            DateEnrolled = DateTime.Today,
            EnrollmentStatus = EnrollmentStatus.Enrolled,
            IsActive = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Student model,
        string? newGuardianFirstName,
        string? newGuardianMiddleName,
        string? newGuardianLastName,
        string? newGuardianRelationship,
        string? newGuardianContactNumber,
        string? newGuardianEmail)
    {
        // Student numbers are allocated by the system. Generate the next one when the
        // field is blank or when a stale/duplicate value was submitted, so encoding a
        // learner never fails because of a number clash.
        if (string.IsNullOrWhiteSpace(model.StudentNumber) ||
            await _db.Students.AnyAsync(s => s.StudentNumber == model.StudentNumber))
        {
            model.StudentNumber = await GenerateStudentNumberAsync();
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            ViewData["Title"] = "Add Student";
            return View(model);
        }

        await ResolveGuardianAsync(model, newGuardianFirstName, newGuardianMiddleName, newGuardianLastName, newGuardianRelationship, newGuardianContactNumber, newGuardianEmail);

        model.IsActive = true;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;
        if (model.DateEnrolled == default) model.DateEnrolled = DateTime.Today;

        _db.Students.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Student", "Student", model.Id, model.FullName);
        await _notify.NotifyAdministratorsAsync(
            "New student added",
            $"{model.FullName} ({model.StudentNumber}) was added to the student register.",
            NotificationType.Info,
            Url.Action(nameof(Details), new { id = model.Id }));

        TempData["Success"] = $"Student {model.FullName} was added.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ------------------------------------------------------------------ Edit
    public async Task<IActionResult> Edit(int id)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        await PopulateListsAsync();
        ViewData["Title"] = $"Edit {student.FullName}";
        return View(student);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        int id,
        Student model,
        string? newGuardianFirstName,
        string? newGuardianMiddleName,
        string? newGuardianLastName,
        string? newGuardianRelationship,
        string? newGuardianContactNumber,
        string? newGuardianEmail)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.StudentNumber)) model.StudentNumber = student.StudentNumber;

        if (await _db.Students.AnyAsync(s => s.StudentNumber == model.StudentNumber && s.Id != id))
        {
            ModelState.AddModelError(nameof(Student.StudentNumber), $"Student number {model.StudentNumber} is already in use.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync();
            model.Id = id;
            ViewData["Title"] = $"Edit {student.FullName}";
            return View(model);
        }

        await ResolveGuardianAsync(model, newGuardianFirstName, newGuardianMiddleName, newGuardianLastName, newGuardianRelationship, newGuardianContactNumber, newGuardianEmail);

        student.StudentNumber = model.StudentNumber;
        student.Lrn = model.Lrn;
        student.FirstName = model.FirstName;
        student.MiddleName = model.MiddleName;
        student.LastName = model.LastName;
        student.Suffix = model.Suffix;
        student.Gender = model.Gender;
        student.BirthDate = model.BirthDate;
        student.Address = model.Address;
        student.ContactNumber = model.ContactNumber;
        student.Email = model.Email;
        student.GuardianId = model.GuardianId;
        student.GradeLevelId = model.GradeLevelId;
        student.SectionId = model.SectionId;
        student.EnrollmentStatus = model.EnrollmentStatus;
        student.DateEnrolled = model.DateEnrolled == default ? student.DateEnrolled : model.DateEnrolled;
        student.IsActive = model.IsActive;
        student.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Student", "Student", student.Id, student.FullName);
        TempData["Success"] = $"Student {student.FullName} was updated.";
        return RedirectToAction(nameof(Details), new { id = student.Id });
    }

    // ---------------------------------------------------------------- Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        var name = student.FullName;
        _db.Students.Remove(student);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Student", "Student", id, name);
        TempData["Success"] = $"Student {name} was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Helpers
    private async Task PopulateListsAsync()
    {
        ViewData["GradeLevels"] = (await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync())
            .Select(g => new SelectListItem(g.Name, g.Id.ToString()))
            .ToList();

        ViewData["Sections"] = (await _db.Sections.Include(s => s.GradeLevel).OrderBy(s => s.Name).ToListAsync())
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString()))
            .ToList();

        ViewData["Guardians"] = (await _db.Guardians.OrderBy(g => g.LastName).ThenBy(g => g.FirstName).ToListAsync())
            .Select(g => new SelectListItem($"{g.FullName} ({g.Relationship})", g.Id.ToString()))
            .ToList();
    }

    private async Task<List<SelectListItem>> BuildGradeLevelOptionsAsync(int? selected)
    {
        var levels = await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync();
        return levels.Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected == g.Id)).ToList();
    }

    private async Task<List<SelectListItem>> BuildSectionOptionsAsync(int? selected)
    {
        var sections = await _db.Sections.Include(s => s.GradeLevel).OrderBy(s => s.Name).ToListAsync();
        return sections.Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), selected == s.Id)).ToList();
    }

    private async Task<string> GenerateStudentNumberAsync()
    {
        var prefix = SchoolInfo.SchoolYear.Split('-')[0] + "-";
        var existing = await _db.Students
            .Where(s => s.StudentNumber.StartsWith(prefix))
            .Select(s => s.StudentNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in existing)
        {
            if (int.TryParse(number[prefix.Length..], out var value) && value > max) max = value;
        }

        return $"{prefix}{max + 1:D4}";
    }

    private async Task ResolveGuardianAsync(
        Student model,
        string? firstName,
        string? middleName,
        string? lastName,
        string? relationship,
        string? contactNumber,
        string? email)
    {
        if (model.GuardianId is > 0)
        {
            return;
        }

        model.GuardianId = null;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return;
        }

        var guardian = new Guardian
        {
            FirstName = firstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
            LastName = lastName.Trim(),
            Relationship = string.IsNullOrWhiteSpace(relationship) ? "Parent" : relationship.Trim(),
            ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : contactNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Guardians.Add(guardian);
        await _db.SaveChangesAsync();
        model.GuardianId = guardian.Id;
    }
}
