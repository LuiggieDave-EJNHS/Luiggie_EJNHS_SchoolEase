using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Builds, previews, prints and exports the school's standard reports.</summary>
[Authorize(Policy = Policies.StaffOnly)]
public class ReportsController : Controller
{
    private readonly IReportService _reports;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ReportsController(
        IReportService reports,
        ICurrentUserService currentUser,
        ApplicationDbContext db,
        IAuditService audit)
    {
        _reports = reports;
        _currentUser = currentUser;
        _db = db;
        _audit = audit;
    }

    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index(ReportFilterViewModel filter)
    {
        await PopulateListsAsync(filter);

        var vm = new ReportPageViewModel { Filter = filter };

        // A query string means the picker or the filter form was submitted.
        if (Request.Query.Count > 0)
        {
            vm.Report = await _reports.BuildAsync(filter, _currentUser.FullName);
        }

        ViewData["Title"] = "Reports";
        return View(vm);
    }

    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Generate(ReportFilterViewModel filter)
    {
        var report = await _reports.BuildAsync(filter, _currentUser.FullName);

        await _audit.LogAsync("Generate Report", "Report", null,
            $"Generated report '{report.Title}' ({filter.ReportType}).");

        var vm = new ReportPageViewModel { Filter = filter, Report = report };
        ViewData["Title"] = report.Title;
        return View(vm);
    }

    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Print(ReportFilterViewModel filter)
    {
        var report = await _reports.BuildAsync(filter, _currentUser.FullName);

        var vm = new ReportPageViewModel { Filter = filter, Report = report, IsPrint = true };
        ViewData["Title"] = report.Title;
        return View(vm);
    }

    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> ExportCsv(ReportFilterViewModel filter)
    {
        var report = await _reports.BuildAsync(filter, _currentUser.FullName);

        await _audit.LogAsync("Export Report", "Report", null,
            $"Exported report '{report.Title}' to CSV.");

        var csv = ReportService.ToCsv(report);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "report.csv");
    }

    // ------------------------------------------------------------------
    private async Task PopulateListsAsync(ReportFilterViewModel filter)
    {
        var gradeLevels = await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();

        filter.GradeLevels = new List<SelectListItem> { new("All grade levels", "") };
        filter.GradeLevels.AddRange(gradeLevels.Select(g =>
            new SelectListItem(g.Name, g.Id.ToString(), filter.GradeLevelId == g.Id)));

        var sections = await _db.Sections
            .Include(s => s.GradeLevel)
            .OrderBy(s => s.GradeLevel!.OrderIndex).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, GradeLevel = s.GradeLevel!.Name })
            .ToListAsync();

        filter.Sections = new List<SelectListItem> { new("All sections", "") };
        filter.Sections.AddRange(sections.Select(s =>
            new SelectListItem($"{s.GradeLevel} - {s.Name}", s.Id.ToString(), filter.SectionId == s.Id)));

        var subjects = await _db.Subjects
            .OrderBy(s => s.Code)
            .Select(s => new { s.Id, s.Code, s.Title })
            .ToListAsync();

        filter.Subjects = new List<SelectListItem> { new("All subjects", "") };
        filter.Subjects.AddRange(subjects.Select(s =>
            new SelectListItem($"{s.Code} - {s.Title}", s.Id.ToString(), filter.SubjectId == s.Id)));

        var teachers = await _db.Teachers
            .OrderBy(t => t.LastName).ThenBy(t => t.FirstName)
            .Select(t => new { t.Id, t.FirstName, t.LastName })
            .ToListAsync();

        filter.Teachers = new List<SelectListItem> { new("All teachers", "") };
        filter.Teachers.AddRange(teachers.Select(t =>
            new SelectListItem($"{t.LastName}, {t.FirstName}", t.Id.ToString(), filter.TeacherId == t.Id)));

        var students = await _db.Students
            .Where(s => s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.StudentNumber })
            .Take(1000)
            .ToListAsync();

        filter.Students = new List<SelectListItem> { new("All students", "") };
        filter.Students.AddRange(students.Select(s =>
            new SelectListItem($"{s.LastName}, {s.FirstName} ({s.StudentNumber})", s.Id.ToString(), filter.StudentId == s.Id)));
    }
}
