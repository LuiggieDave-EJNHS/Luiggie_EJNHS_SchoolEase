using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>Self-service portal for learners.</summary>
[Authorize(Roles = $"{Roles.Student},{Roles.FormerStudent}")]
public class StudentController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDashboardService _dashboard;
    private readonly IFileStorage _storage;
    private readonly IAuditService _audit;

    public StudentController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IDashboardService dashboard,
        IFileStorage storage,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _dashboard = dashboard;
        _storage = storage;
        _audit = audit;
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        var userId = _currentUser.UserId!;

        // Auto-link a minimal record for Former Students if missing
        await EnsureStudentAsync();

        var vm = await _dashboard.BuildStudentDashboardAsync(userId);

        if (vm.Student?.SectionId is not null)
        {
            var sectionId = vm.Student.SectionId.Value;
            ViewData["AdviserName"] = await _db.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Adviser!.FullName)
                .FirstOrDefaultAsync();
        }

        ViewData["Title"] = "My Dashboard";
        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Profile()
    {
        ViewData["Title"] = "My Profile";
        return View(await LoadStudentAsync());
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Schedule()
    {
        var vm = new StudentScheduleViewModel();
        var student = await LoadStudentAsync();

        vm.Student = student;
        vm.HasSection = student?.SectionId is not null;

        if (student?.SectionId is not null)
        {
            var schedule = (await _db.ClassSchedules
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .Include(c => c.Section)
                .Where(c => c.SectionId == student.SectionId)
                .ToListAsync())
                .OrderBy(c => c.StartTime)
                .ToList();

            vm.Weekly = schedule;
            vm.Today = schedule.Where(c => c.DayOfWeek == DateTime.Today.DayOfWeek).ToList();
        }

        ViewData["Title"] = "My Schedule";
        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Attendance(int? month, int? year, int page = 1)
    {
        var vm = new StudentAttendanceViewModel { Month = month, Year = year };
        var student = await LoadStudentAsync();
        vm.Student = student;

        var today = DateTime.Today;
        var years = await _db.AttendanceRecords
            .Where(a => a.StudentId == (student == null ? 0 : student.Id))
            .Select(a => a.Date.Year)
            .Distinct()
            .ToListAsync();

        if (years.Count == 0) years.Add(today.Year);
        years.Sort((a, b) => b.CompareTo(a));

        vm.Years = years
            .Select(y => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                y.ToString(), y.ToString(), year == y))
            .ToList();

        vm.Months = Enumerable.Range(1, 12)
            .Select(m => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                new DateTime(2000, m, 1).ToString("MMMM"), m.ToString(), month == m))
            .ToList();

        if (student is null)
        {
            vm.Records = new PagedList<AttendanceRecord>(new List<AttendanceRecord>(), 0, 1, PageSize);
            ViewData["Title"] = "My Attendance";
            return View(vm);
        }

        var query = _db.AttendanceRecords
            .Include(a => a.Subject)
            .Include(a => a.RecordedBy)
            .Where(a => a.StudentId == student.Id);

        if (year is > 0) query = query.Where(a => a.Date.Year == year);
        if (month is > 0) query = query.Where(a => a.Date.Month == month);

        var all = await query
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.Id)
            .ToListAsync();

        vm.PresentDays = all.Count(a => a.Status == AttendanceStatus.Present);
        vm.LateDays = all.Count(a => a.Status == AttendanceStatus.Late);
        vm.AbsentDays = all.Count(a => a.Status == AttendanceStatus.Absent);
        vm.ExcusedDays = all.Count(a => a.Status == AttendanceStatus.Excused);
        vm.AttendanceRate = all.Count == 0
            ? 0
            : Math.Round((vm.PresentDays + vm.LateDays) * 100.0 / all.Count, 1);

        page = page < 1 ? 1 : page;
        vm.Records = new PagedList<AttendanceRecord>(
            all.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
            all.Count, page, PageSize);

        // ----- trend (last 7 days) -----
        var trendStart = today.AddDays(-6);
        var dates = Enumerable.Range(0, 7).Select(i => trendStart.AddDays(i)).ToList();
        var recent = await _db.AttendanceRecords
            .Where(a => a.StudentId == student.Id && a.Date >= trendStart)
            .ToListAsync();

        vm.Trend = new ChartData
        {
            Title = "My Attendance (Last 7 Days)",
            Labels = dates.Select(d => d.ToString("MMM d")).ToList(),
            Values = dates.Select(d => (double)recent.Count(a =>
                a.Date.Date == d && a.Status is AttendanceStatus.Present or AttendanceStatus.Late)).ToList(),
            Colors = dates.Select(_ => "#800000").ToList()
        };

        ViewData["Title"] = "My Attendance";
        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Grades()
    {
        var vm = new MyGradesViewModel();
        var student = await LoadStudentAsync();
        vm.Student = student;

        if (student is not null)
        {
            var grades = await _db.GradeRecords
                .Include(g => g.Subject)
                .Where(g => g.StudentId == student.Id)
                .ToListAsync();

            vm.Subjects = grades
                .GroupBy(g => g.SubjectId)
                .Select(grp =>
                {
                    var finalGrades = grp
                        .Where(g => g.FinalGrade.HasValue)
                        .Select(g => g.FinalGrade!.Value)
                        .ToList();

                    return new StudentSubjectGradeRow
                    {
                        Subject = grp.First().Subject,
                        Terms = grp
                            .GroupBy(g => g.Term)
                            .ToDictionary(t => t.Key, t => t.First().FinalGrade),
                        Average = finalGrades.Count == 0
                            ? null
                            : Math.Round(finalGrades.Average(), 2)
                    };
                })
                .OrderBy(r => r.Subject.Code)
                .ToList();

            var graded = grades.Where(g => g.FinalGrade.HasValue).Select(g => g.FinalGrade!.Value).ToList();
            vm.GeneralAverage = graded.Count == 0 ? null : Math.Round(graded.Average(), 2);
            vm.SchoolYear = grades.FirstOrDefault()?.SchoolYear ?? SchoolInfo.SchoolYear;
        }

        ViewData["Title"] = "My Grades";
        return View(vm);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Requests()
    {
        ViewData["Title"] = "My Document Requests";

        var student = await LoadStudentAsync();
        if (student is null) return View(new List<DocumentRequest>());

        var requests = await _db.DocumentRequests
            .Include(r => r.DocumentType)
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        return View(requests);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> Documents()
    {
        var vm = new MyDocumentsViewModel();
        var student = await LoadStudentAsync();
        vm.Student = student;

        if (student is not null)
        {
            vm.Documents = await _db.StudentDocuments
                .Where(d => d.StudentId == student.Id && d.IsVisibleToStudent)
                .OrderBy(d => d.Category)
                .ThenByDescending(d => d.CreatedAt)
                .Select(d => new StudentDocument
                {
                    Id = d.Id,
                    StudentId = d.StudentId,
                    Title = d.Title,
                    Category = d.Category,
                    Description = d.Description,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    FileSize = d.FileSize,
                    StorageKey = d.StorageKey,
                    IsVisibleToStudent = d.IsVisibleToStudent,
                    SchoolYear = d.SchoolYear,
                    UploaderName = d.UploaderName,
                    DownloadCount = d.DownloadCount,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync();
        }

        ViewData["Title"] = "My Records";
        return View(vm);
    }

    // ------------------------------------------------------------------
    /// <summary>Lets the learner scan or pick a file and add it to their own 201 file.</summary>
    [HttpGet]
    public async Task<IActionResult> UploadDocument()
    {
        var student = await EnsureStudentAsync();
        if (student is null) return NotFound();

        ViewData["Title"] = "Upload a record";
        return View(new StudentDocumentFormViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(StudentDocumentFormViewModel model)
    {
        var student = await EnsureStudentAsync();
        if (student is null) return NotFound();

        var upload = model.File ?? model.Camera;
        DocumentUpload.Validate(upload, ModelState, nameof(model.File), required: true);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Upload a record";
            return View(model);
        }

        var document = new StudentDocument
        {
            StudentId = student.Id,
            Title = model.Title.Trim(),
            Category = model.Category,
            Description = model.Description?.Trim(),
            IsVisibleToStudent = true,
            SchoolYear = SchoolInfo.SchoolYear,
            UploaderName = _currentUser.FullName,
            UploadedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await DocumentUpload.StoreAsync(document, upload!, _storage);

        _db.StudentDocuments.Add(document);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Upload Own Document", "StudentDocument", document.Id,
            $"{document.Title} ({document.Category.DisplayName()}) uploaded by the learner.");

        TempData["Success"] = "Your record was added to your 201 file.";
        return RedirectToAction(nameof(Documents));
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> PreviewDocument(int id)
    {
        var document = await LoadOwnDocumentAsync(id);
        if (document is null) return NotFound();

        var bytes = await ReadDocumentBytesAsync(document);
        if (bytes.Length == 0) return NotFound();

        // Allow this response to be framed same-origin so the in-page preview works.
        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self';";

        return File(bytes, document.ContentType, enableRangeProcessing: true);
    }

    // ------------------------------------------------------------------
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var document = await LoadOwnDocumentAsync(id);
        if (document is null) return NotFound();

        var bytes = await ReadDocumentBytesAsync(document);
        if (bytes.Length == 0) return NotFound();

        document.DownloadCount++;
        await _db.SaveChangesAsync();

        return File(bytes, document.ContentType, document.FileName);
    }

    /// <summary>Loads a record from the current learner's own 201 file, honouring visibility.</summary>
    private async Task<StudentDocument?> LoadOwnDocumentAsync(int id)
    {
        var student = await LoadStudentAsync();
        if (student is null) return null;

        return await _db.StudentDocuments
            .FirstOrDefaultAsync(d => d.Id == id && d.StudentId == student.Id && d.IsVisibleToStudent);
    }

    private async Task<byte[]> ReadDocumentBytesAsync(StudentDocument document)
        => document.StorageKey is not null && _storage.IsConfigured
            ? await _storage.DownloadAsync(document.StorageKey)
            : document.FileContent;

    // ------------------------------------------------------------------
    /// <summary>
    /// Loads the signed-in learner's record, creating a minimal one for a former student
    /// who has none so self-service pages (records, uploads) keep working.
    /// </summary>
    private async Task<Student?> EnsureStudentAsync()
    {
        var student = await LoadStudentAsync();
        if (student is not null) return student;
        if (!_currentUser.IsFormerStudent) return null;

        var names = _currentUser.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        student = new Student
        {
            UserId = _currentUser.UserId!,
            FirstName = names.FirstOrDefault() ?? "Former",
            LastName = names.Length > 1 ? string.Join(" ", names.Skip(1)) : "Student",
            Email = _currentUser.UserName,
            EnrollmentStatus = EnrollmentStatus.Graduated,
            IsActive = true,
            StudentNumber = $"ALM-{DateTime.UtcNow:yyyyMMddHHmmss}"
        };

        _db.Students.Add(student);
        await _db.SaveChangesAsync();
        return student;
    }

    private Task<Student?> LoadStudentAsync()
    {
        var uid = _currentUser.UserId;
        if (uid is null) return Task.FromResult<Student?>(null);

        return _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section).ThenInclude(sec => sec!.Adviser)
            .Include(s => s.Guardian)
            .FirstOrDefaultAsync(s => s.UserId == uid);
    }
}
