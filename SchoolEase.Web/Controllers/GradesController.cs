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

/// <summary>Term grade encoding, overview and printable academic records.</summary>
[Authorize(Policy = Policies.TeacherOrAdmin)]
public class GradesController : Controller
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public GradesController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    // -----------------------------------------------------------------------
    // Overview
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Index(int? sectionId, int? subjectId, int? term, int? gradeLevelId, int page = 1)
    {
        var query = _db.GradeRecords
            .Include(g => g.Student)
            .Include(g => g.Subject)
            .Include(g => g.Section).ThenInclude(s => s.GradeLevel)
            .AsQueryable();

        if (sectionId.HasValue) query = query.Where(g => g.SectionId == sectionId.Value);
        if (subjectId.HasValue) query = query.Where(g => g.SubjectId == subjectId.Value);
        if (term.HasValue) query = query.Where(g => g.Term == term.Value);
        if (gradeLevelId.HasValue) query = query.Where(g => g.Section.GradeLevelId == gradeLevelId.Value);

        var totalRecords = await query.CountAsync();
        var graded = await query.CountAsync(g => g.FinalGrade != null);
        var classAverage = await query.Where(g => g.FinalGrade != null)
            .AverageAsync(g => (double?)g.FinalGrade) ?? 0;
        var passed = await query.CountAsync(g => g.FinalGrade >= 75);
        var failed = await query.CountAsync(g => g.FinalGrade < 75);

        var ordered = query
            .OrderBy(g => g.Section.Name)
            .ThenBy(g => g.Subject.Code)
            .ThenBy(g => g.Student.LastName)
            .ThenBy(g => g.Student.FirstName);

        var records = await PagedList<GradeRecord>.CreateAsync(ordered, page, PageSize);

        var vm = new GradeIndexViewModel
        {
            Records = records,
            SectionId = sectionId,
            SubjectId = subjectId,
            Term = term,
            GradeLevelId = gradeLevelId,
            TotalRecords = totalRecords,
            GradedCount = graded,
            ClassAverage = Math.Round(classAverage, 2),
            PassedCount = passed,
            FailedCount = failed,
            Sections = await SectionOptionsAsync(sectionId),
            Subjects = await SubjectOptionsAsync(subjectId),
            GradeLevels = await GradeLevelOptionsAsync(gradeLevelId),
            SubjectAverages = await BuildSubjectAveragesAsync(query)
        };

        ViewData["Title"] = "Grades";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = records.PageIndex,
            TotalPages = records.TotalPages,
            TotalCount = records.TotalCount,
            PageSize = records.PageSize,
            Action = nameof(Index),
            Controller = "Grades",
            RouteValues = new Dictionary<string, string?>
            {
                ["sectionId"] = sectionId?.ToString(),
                ["subjectId"] = subjectId?.ToString(),
                ["term"] = term?.ToString(),
                ["gradeLevelId"] = gradeLevelId?.ToString()
            }
        };

        var sectionLabel = sectionId?.ToString() ?? "all";
        var subjectLabel = subjectId?.ToString() ?? "all";
        var termLabel = term?.ToString() ?? "all";
        await _audit.LogAsync("Viewed grade records", "GradeRecord", null,
            $"Filters: section={sectionLabel}, subject={subjectLabel}, term={termLabel}");

        return View(vm);
    }

    // -----------------------------------------------------------------------
    // Encoding sheet
    // -----------------------------------------------------------------------
    public async Task<IActionResult> Encode(int sectionId, int subjectId, int term = 1)
    {
        var vm = await BuildEncodeViewModelAsync(sectionId, subjectId, term);
        if (vm is null)
        {
            TempData["Error"] = "Select a valid section and subject to encode grades.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Encode Grades";
        await _audit.LogAsync("Viewed grade encoding sheet", "GradeRecord", null,
            $"Section {sectionId}, subject {subjectId}, term {term}");
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Save(int sectionId, int subjectId, int term, List<GradeRowInput> rows)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
        if (subject is null)
        {
            TempData["Error"] = "The selected subject could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var schoolYear = SchoolInfo.SchoolYear;
        var teacherId = await CurrentTeacherIdAsync();

        var existing = await _db.GradeRecords
            .Where(g => g.SectionId == sectionId && g.SubjectId == subjectId && g.Term == term
                && g.SchoolYear == schoolYear)
            .ToListAsync();

        var count = 0;
        foreach (var row in rows ?? new List<GradeRowInput>())
        {
            var record = existing.FirstOrDefault(g => g.StudentId == row.StudentId);
            if (record is null)
            {
                record = new GradeRecord
                {
                    StudentId = row.StudentId,
                    SubjectId = subjectId,
                    SectionId = sectionId,
                    Term = term,
                    SchoolYear = schoolYear
                };
                _db.GradeRecords.Add(record);
            }

            record.WrittenWork = row.WrittenWork;
            record.PerformanceTask = row.PerformanceTask;
            record.TermAssessment = row.TermAssessment;
            record.Remarks = row.Remarks;

            record.FinalGrade = row.WrittenWork.HasValue && row.PerformanceTask.HasValue && row.TermAssessment.HasValue
                ? record.ComputeWeighted()
                : null;

            record.EncodedByUserId = _currentUser.UserId;
            record.EncodedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            if (teacherId.HasValue) record.TeacherId = teacherId.Value;
            count++;
        }

        await _db.SaveChangesAsync();

        // Let linked learners know their grades changed.
        var studentIds = (rows ?? new List<GradeRowInput>()).Select(r => r.StudentId).Distinct().ToList();
        var linked = await _db.Students.Where(s => studentIds.Contains(s.Id) && s.UserId != null).ToListAsync();
        foreach (var student in linked)
        {
            await _notify.NotifyAsync(
                student.UserId!,
                "Grades updated",
                $"Your {subject.Code} grade for Term {term} has been updated.",
                NotificationType.Info,
                Url.Action(nameof(StudentGrades), new { studentId = student.Id }));
        }

        await _audit.LogAsync("Encoded grades", "GradeRecord", sectionId,
            $"{count} record(s) for section {sectionId}, subject {subjectId}, term {term}");

        TempData["Success"] = $"{count} grade record(s) saved for {subject.Code}, Term {term}.";
        return RedirectToAction(nameof(Encode), new { sectionId, subjectId, term });
    }

    // -----------------------------------------------------------------------
    // Printable academic record
    // -----------------------------------------------------------------------
    public async Task<IActionResult> StudentGrades(int studentId)
    {
        var student = await _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student is null)
        {
            return NotFound();
        }

        var grades = await _db.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == studentId)
            .OrderBy(g => g.Subject.Code).ThenBy(g => g.Term)
            .ToListAsync();

        var groups = grades
            .GroupBy(g => g.Subject)
            .OrderBy(sg => sg.Key.Code)
            .Select(sg =>
            {
                var terms = sg.ToDictionary(g => g.Term, g => g.FinalGrade);
                var graded = terms.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                return new SubjectGradeGroup
                {
                    SubjectCode = sg.Key.Code,
                    SubjectTitle = sg.Key.Title,
                    Terms = terms,
                    Average = graded.Count == 0 ? null : Math.Round(graded.Average(), 2)
                };
            })
            .ToList();

        var subjectAverages = groups.Where(g => g.Average.HasValue).Select(g => g.Average!.Value).ToList();

        var vm = new StudentGradesViewModel
        {
            Student = student,
            StudentName = student.FullName,
            ClassLabel = student.ClassLabel,
            SchoolYear = SchoolInfo.SchoolYear,
            Subjects = groups,
            GeneralAverage = subjectAverages.Count == 0 ? null : Math.Round(subjectAverages.Average(), 2)
        };

        ViewData["Title"] = "Student Grades";
        await _audit.LogAsync("Viewed student academic record", "Student", studentId, student.FullName);
        return View(vm);
    }

    public async Task<IActionResult> Print(int sectionId, int subjectId, int term)
    {
        var section = await _db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == sectionId);
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
        if (section is null || subject is null)
        {
            TempData["Error"] = "Select a valid section and subject to print.";
            return RedirectToAction(nameof(Index));
        }

        var students = await _db.Students
            .Where(s => s.SectionId == sectionId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var existing = await _db.GradeRecords
            .Where(g => g.SectionId == sectionId && g.SubjectId == subjectId && g.Term == term
                && g.SchoolYear == SchoolInfo.SchoolYear)
            .ToListAsync();

        var rows = students.Select(s =>
        {
            var rec = existing.FirstOrDefault(g => g.StudentId == s.Id);
            return new GradeEncodeRow
            {
                StudentId = s.Id,
                StudentNumber = s.StudentNumber,
                StudentName = s.FullName,
                WrittenWork = rec?.WrittenWork,
                PerformanceTask = rec?.PerformanceTask,
                TermAssessment = rec?.TermAssessment,
                FinalGrade = rec?.FinalGrade,
                Remarks = rec?.Remarks
            };
        }).ToList();

        var gradedValues = rows.Where(r => r.FinalGrade.HasValue).Select(r => r.FinalGrade!.Value).ToList();

        var vm = new GradePrintViewModel
        {
            SectionLabel = section.DisplayName,
            SubjectLabel = subject.DisplayName,
            Term = term,
            SchoolYear = SchoolInfo.SchoolYear,
            GeneratedAt = DateTime.Now,
            ClassAverage = gradedValues.Count == 0 ? null : Math.Round(gradedValues.Average(), 2),
            Rows = rows
        };

        ViewData["Title"] = "Class Record";
        await _audit.LogAsync("Printed class record", "GradeRecord", sectionId,
            $"Section {sectionId}, subject {subjectId}, term {term}");
        return View(vm);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private async Task<GradeEncodeViewModel?> BuildEncodeViewModelAsync(int sectionId, int subjectId, int term)
    {
        var section = await _db.Sections.Include(s => s.GradeLevel).FirstOrDefaultAsync(s => s.Id == sectionId);
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId);
        if (section is null || subject is null) return null;

        var students = await _db.Students
            .Where(s => s.SectionId == sectionId)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var existing = await _db.GradeRecords
            .Where(g => g.SectionId == sectionId && g.SubjectId == subjectId && g.Term == term
                && g.SchoolYear == SchoolInfo.SchoolYear)
            .ToListAsync();

        return new GradeEncodeViewModel
        {
            SectionId = sectionId,
            SubjectId = subjectId,
            Term = term,
            SectionLabel = section.DisplayName,
            SubjectLabel = subject.DisplayName,
            Rows = students.Select(s =>
            {
                var rec = existing.FirstOrDefault(g => g.StudentId == s.Id);
                return new GradeEncodeRow
                {
                    StudentId = s.Id,
                    StudentNumber = s.StudentNumber,
                    StudentName = s.FullName,
                    WrittenWork = rec?.WrittenWork,
                    PerformanceTask = rec?.PerformanceTask,
                    TermAssessment = rec?.TermAssessment,
                    FinalGrade = rec?.FinalGrade,
                    Remarks = rec?.Remarks
                };
            }).ToList(),
            Sections = await SectionOptionsAsync(sectionId),
            Subjects = await SubjectOptionsAsync(subjectId)
        };
    }

    private async Task<ChartData> BuildSubjectAveragesAsync(IQueryable<GradeRecord> query)
    {
        var rows = await query
            .Where(g => g.FinalGrade != null)
            .GroupBy(g => g.Subject.Code)
            .Select(gr => new { Label = gr.Key, Average = gr.Average(g => g.FinalGrade ?? 0) })
            .OrderBy(x => x.Label)
            .ToListAsync();

        var data = new ChartData { Title = "Average Final Grade per Subject" };
        foreach (var row in rows)
        {
            data.Labels.Add(row.Label);
            data.Values.Add(Math.Round(row.Average, 2));
        }

        return data;
    }

    private async Task<int?> CurrentTeacherIdAsync()
    {
        if (!_currentUser.IsTeacher) return null;
        var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == _currentUser.UserId);
        return teacher?.Id;
    }

    private async Task<List<SelectListItem>> SectionOptionsAsync(int? selected)
    {
        var sections = await _db.Sections
            .Include(s => s.GradeLevel)
            .OrderBy(s => s.GradeLevel.OrderIndex).ThenBy(s => s.Name)
            .ToListAsync();

        return sections
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), selected == s.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> SubjectOptionsAsync(int? selected)
    {
        var subjects = await _db.Subjects
            .OrderBy(s => s.Code)
            .ToListAsync();

        return subjects
            .Select(s => new SelectListItem(s.DisplayName, s.Id.ToString(), selected == s.Id))
            .ToList();
    }

    private async Task<List<SelectListItem>> GradeLevelOptionsAsync(int? selected)
    {
        var levels = await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync();
        return levels
            .Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected == g.Id))
            .ToList();
    }
}
