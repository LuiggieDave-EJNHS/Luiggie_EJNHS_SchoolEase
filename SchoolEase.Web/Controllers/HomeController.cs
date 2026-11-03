using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext db, ICurrentUserService currentUser, ILogger<HomeController> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        if (_currentUser.IsAuthenticated)
        {
            return Redirect(_currentUser.HomeAction);
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        var vm = new SearchViewModel { Query = (q ?? string.Empty).Trim() };

        if (vm.Query.Length >= 2)
        {
            var term = vm.Query;
            var isAdmin = _currentUser.IsAdministrator;
            var isStaff = _currentUser.HasSchoolWideAccess || _currentUser.IsRegistrar || _currentUser.IsTeacher;

            if (isStaff)
            {
                var students = await _db.Students
                    .Include(s => s.Section)
                    .Where(s => s.FirstName.Contains(term) || s.LastName.Contains(term) || s.StudentNumber.Contains(term) || (s.Lrn != null && s.Lrn.Contains(term)))
                    .OrderBy(s => s.LastName)
                    .Take(8)
                    .ToListAsync();

                vm.Hits.AddRange(students.Select(s => new SearchHit
                {
                    Title = s.FullName,
                    Subtitle = $"{s.StudentNumber} • {s.ClassLabel}",
                    Category = "Student",
                    Icon = "bi-person",
                    Url = Url.Action("Details", "Students", new { id = s.Id })!
                }));

                var teachers = await _db.Teachers
                    .Where(t => t.FirstName.Contains(term) || t.LastName.Contains(term) || t.EmployeeNumber.Contains(term))
                    .OrderBy(t => t.LastName)
                    .Take(6)
                    .ToListAsync();

                vm.Hits.AddRange(teachers.Select(t => new SearchHit
                {
                    Title = t.FullName,
                    Subtitle = $"{t.EmployeeNumber} • {t.Department ?? "Faculty"}",
                    Category = "Teacher",
                    Icon = "bi-person-video3",
                    Url = isAdmin ? Url.Action("Details", "Teachers", new { id = t.Id })! : Url.Action("Index", "Teachers")!
                }));

                var subjects = await _db.Subjects
                    .Where(s => s.Code.Contains(term) || s.Title.Contains(term))
                    .OrderBy(s => s.Code)
                    .Take(6)
                    .ToListAsync();

                vm.Hits.AddRange(subjects.Select(s => new SearchHit
                {
                    Title = s.DisplayName,
                    Subtitle = $"{s.Units:0.#} unit(s)",
                    Category = "Subject",
                    Icon = "bi-journal-bookmark",
                    Url = Url.Action("Index", "Subjects")!
                }));

                var sections = await _db.Sections
                    .Include(s => s.GradeLevel)
                    .Where(s => s.Name.Contains(term) || s.Code.Contains(term))
                    .Take(6)
                    .ToListAsync();

                vm.Hits.AddRange(sections.Select(s => new SearchHit
                {
                    Title = s.DisplayName,
                    Subtitle = $"{s.TrackStrand ?? "Section"} • Room {s.Room ?? "TBA"}",
                    Category = "Section",
                    Icon = "bi-diagram-3",
                    Url = Url.Action("Index", "Sections")!
                }));
            }

            if (isAdmin || _currentUser.IsRegistrar)
            {
                var requests = await _db.DocumentRequests
                    .Include(r => r.Student)
                    .Include(r => r.DocumentType)
                    .Where(r => r.RequestNumber.Contains(term) || r.Student!.LastName.Contains(term))
                    .OrderByDescending(r => r.RequestedAt)
                    .Take(6)
                    .ToListAsync();

                vm.Hits.AddRange(requests.Select(r => new SearchHit
                {
                    Title = $"{r.RequestNumber} — {r.DocumentType?.Name}",
                    Subtitle = $"{r.Student?.FullName} • {r.Status.DisplayName()}",
                    Category = "Request",
                    Icon = "bi-file-earmark-text",
                    Url = Url.Action("Details", "DocumentRequests", new { id = r.Id })!
                }));
            }
        }

        return View(vm);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public new IActionResult StatusCode(int code = 404)
    {
        ViewData["StatusCode"] = code;
        ViewData["Title"] = code switch
        {
            401 => "Unauthorized",
            403 => "Access denied",
            404 => "Page not found",
            500 => "Server error",
            _ => "Something went wrong"
        };
        Response.StatusCode = code;
        return View();
    }
}
