using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Self-service portal for teaching personnel.</summary>
[Authorize(Roles = Roles.Teacher)]
public class TeacherController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDashboardService _dashboard;
    private readonly IAuditService _audit;

    public TeacherController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IDashboardService dashboard,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _dashboard = dashboard;
        _audit = audit;
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var uid = _currentUser.UserId;
        var vm = await _dashboard.BuildTeacherDashboardAsync(uid!);

        ViewData["Title"] = "Teacher Dashboard";
        ViewData["NoTeacherRecord"] = uid is null ||
            !await _db.Teachers.AnyAsync(t => t.UserId == uid);

        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Classes()
    {
        var uid = _currentUser.UserId;
        var vm = new TeacherClassesViewModel();

        var teacher = uid is null
            ? null
            : await _db.Teachers
                .Include(t => t.AdvisorySections).ThenInclude(s => s.GradeLevel)
                .FirstOrDefaultAsync(t => t.UserId == uid);

        if (teacher is not null)
        {
            vm.Teacher = teacher;

            var assignments = await _db.TeacherSubjectAssignments
                .Include(a => a.Subject)
                .Include(a => a.Section).ThenInclude(s => s.GradeLevel)
                .Where(a => a.TeacherId == teacher.Id)
                .ToListAsync();

            var advisory = teacher.AdvisorySections;
            var sectionIds = assignments.Select(a => a.SectionId)
                .Concat(advisory.Select(s => s.Id))
                .Distinct()
                .ToList();

            var sections = await _db.Sections
                .Include(s => s.GradeLevel)
                .Where(s => sectionIds.Contains(s.Id))
                .ToListAsync();

            var counts = await _db.Students
                .Where(s => s.IsActive && s.SectionId != null && sectionIds.Contains(s.SectionId.Value))
                .GroupBy(s => s.SectionId!.Value)
                .Select(g => new { SectionId = g.Key, Count = g.Count() })
                .ToListAsync();

            vm.Classes = sections
                .Select(sec => new TeacherClassCard
                {
                    Section = sec,
                    IsAdviser = advisory.Any(a => a.Id == sec.Id),
                    StudentCount = counts.FirstOrDefault(c => c.SectionId == sec.Id)?.Count ?? 0,
                    Subjects = assignments
                        .Where(a => a.SectionId == sec.Id)
                        .Select(a => a.Subject)
                        .DistinctBy(s => s.Id)
                        .OrderBy(s => s.Code)
                        .ToList()
                })
                .OrderBy(c => c.Section.GradeLevel.OrderIndex)
                .ThenBy(c => c.Section.Name)
                .ToList();
        }

        ViewData["Title"] = "My Classes";
        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Schedule()
    {
        var uid = _currentUser.UserId;
        var vm = new TeacherScheduleViewModel();

        var teacher = uid is null
            ? null
            : await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == uid);

        if (teacher is not null)
        {
            vm.TeacherName = teacher.FullName;
            vm.Entries = (await _db.ClassSchedules
                .Include(c => c.Subject)
                .Include(c => c.Section)
                .Where(c => c.TeacherId == teacher.Id)
                .ToListAsync())
                .OrderBy(c => c.StartTime)
                .ToList();
            vm.WeeklyHours = vm.Entries.Sum(c => c.DurationHours);
        }

        ViewData["Title"] = "My Schedule";
        return View(vm);
    }

    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        ViewData["Title"] = "My Profile";
        return View(await BuildProfileAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(TeacherProfileViewModel model)
    {
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);
        if (teacher is null)
        {
            TempData["Error"] = "No teaching record is linked to your account. Please contact the administrator.";
            return RedirectToAction(nameof(Profile));
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "My Profile";
            return View(await BuildProfileAsync(model.Form));
        }

        teacher.ContactNumber = model.Form.ContactNumber?.Trim();
        teacher.Email = model.Form.Email?.Trim();
        teacher.Address = model.Form.Address?.Trim();
        teacher.Specialization = model.Form.Specialization?.Trim();
        teacher.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Profile", "Teacher", teacher.Id,
            $"{teacher.FullName} updated their contact details.");

        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    // ------------------------------------------------------------------
    private async Task<TeacherProfileViewModel> BuildProfileAsync(TeacherProfileEditViewModel? form = null)
    {
        var vm = new TeacherProfileViewModel();

        var teacher = await _db.Teachers
            .Include(t => t.AdvisorySections)
            .Include(t => t.SubjectAssignments)
            .FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);

        if (teacher is not null)
        {
            vm.Teacher = teacher;
            vm.AdvisoryCount = teacher.AdvisorySections.Count;
            vm.AssignedSubjectCount = teacher.SubjectAssignments.Select(a => a.SubjectId).Distinct().Count();
            vm.AssignedSectionCount = teacher.SubjectAssignments.Select(a => a.SectionId).Distinct().Count();

            vm.Form = form ?? new TeacherProfileEditViewModel
            {
                ContactNumber = teacher.ContactNumber,
                Email = teacher.Email,
                Address = teacher.Address,
                Specialization = teacher.Specialization
            };
        }

        return vm;
    }
}
