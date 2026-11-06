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

/// <summary>
/// Library of Learning Activity Sheets (LAS). Teachers upload PDF activity
/// sheets; students and staff browse and download them.
/// </summary>
[Authorize]
public class LearningActivitySheetsController : Controller
{
    private const int PageSize = 12;
    private const long MaxFileSize = 15 * 1024 * 1024; // 15 MB

    private static readonly string[] AllowedExtensions = { ".pdf" };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public LearningActivitySheetsController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Only teachers may upload and manage activity sheets.</summary>
    private bool CanUpload => _currentUser.IsTeacher;

    // ------------------------------------------------------------------
    // Library (everyone except former students)
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index(int? subjectId, int? gradeLevelId, int? term, string? keyword, int page = 1)
    {
        if (_currentUser.IsFormerStudent) return Forbid();

        var query = ApplyVisibility(_db.LearningActivitySheets
            .Include(x => x.Subject)
            .Include(x => x.GradeLevel));

        if (subjectId is > 0) query = query.Where(x => x.SubjectId == subjectId);
        if (gradeLevelId is > 0) query = query.Where(x => x.GradeLevelId == gradeLevelId);
        if (term is > 0) query = query.Where(x => x.Term == term);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                x.Title.Contains(k)
                || (x.Description != null && x.Description.Contains(k))
                || (x.MelcCode != null && x.MelcCode.Contains(k))
                || (x.Competency != null && x.Competency.Contains(k))
                || (x.Subject != null && x.Subject.Title.Contains(k)));
        }

        var paged = await PagedList<LearningActivitySheet>.CreateAsync(
            query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id), page, PageSize);

        var visible = ApplyVisibility(_db.LearningActivitySheets);
        var total = await visible.CountAsync();
        var published = await visible.CountAsync(x => x.IsPublished);
        var downloads = await visible.SumAsync(x => (int?)x.DownloadCount) ?? 0;

        var vm = new ActivitySheetListViewModel
        {
            Sheets = paged,
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId,
            Term = term,
            Keyword = keyword,
            Subjects = await SubjectOptionsAsync(),
            GradeLevels = await GradeLevelOptionsAsync(),
            CanUpload = CanUpload,
            IsFiltered = subjectId is > 0 || gradeLevelId is > 0 || term is > 0 || !string.IsNullOrWhiteSpace(keyword),
            TotalSheets = total,
            PublishedSheets = published,
            DraftSheets = total - published,
            TotalDownloads = downloads
        };

        ViewData["Title"] = "Learning Activity Sheets";
        ViewData["Section"] = "Academics";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "LearningActivitySheets",
            RouteValues = new Dictionary<string, string?>
            {
                ["subjectId"] = subjectId?.ToString(),
                ["gradeLevelId"] = gradeLevelId?.ToString(),
                ["term"] = term?.ToString(),
                ["keyword"] = keyword
            }
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (_currentUser.IsFormerStudent) return Forbid();

        var sheet = await _db.LearningActivitySheets
            .Include(x => x.Subject)
            .Include(x => x.GradeLevel)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (sheet is null) return NotFound();
        if (!sheet.IsPublished && !CanEdit(sheet)) return Forbid();

        ViewData["Title"] = sheet.Title;
        ViewData["Section"] = "Academics";
        ViewData["CanManage"] = CanEdit(sheet);
        return View(sheet);
    }

    public async Task<IActionResult> Download(int id)
    {
        if (_currentUser.IsFormerStudent) return Forbid();

        var sheet = await _db.LearningActivitySheets.FirstOrDefaultAsync(x => x.Id == id);
        if (sheet is null) return NotFound();
        if (!sheet.IsPublished && !CanEdit(sheet)) return Forbid();

        sheet.DownloadCount++;
        await _db.SaveChangesAsync();

        return File(sheet.FileContent, sheet.ContentType, sheet.FileName);
    }

    // ------------------------------------------------------------------
    // Upload (teachers only)
    // ------------------------------------------------------------------
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ActivitySheetFormViewModel();
        await PopulateOptionsAsync(model);

        ViewData["Title"] = "Upload Activity Sheet";
        ViewData["Section"] = "Academics";
        return View(model);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<IActionResult> Create(ActivitySheetFormViewModel model)
    {
        ValidateFile(model.File, required: true);
        await ValidateSubjectAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            ViewData["Title"] = "Upload Activity Sheet";
            ViewData["Section"] = "Academics";
            return View(model);
        }

        var file = model.File!;
        var sheet = new LearningActivitySheet
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            SubjectId = model.SubjectId,
            GradeLevelId = model.GradeLevelId is > 0 ? model.GradeLevelId : null,
            Term = model.Term,
            WeekNumber = model.WeekNumber,
            MelcCode = model.MelcCode?.Trim(),
            Competency = model.Competency?.Trim(),
            FileName = Path.GetFileName(file.FileName),
            ContentType = "application/pdf",
            FileSize = file.Length,
            FileContent = await ReadFileAsync(file),
            IsPublished = model.IsPublished,
            SchoolYear = SchoolInfo.SchoolYear,
            UploaderName = _currentUser.FullName,
            UploadedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.LearningActivitySheets.Add(sheet);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Upload Activity Sheet", "LearningActivitySheet", sheet.Id,
            $"Uploaded \"{sheet.Title}\" ({sheet.FileName}).");
        TempData["Success"] = "Activity sheet uploaded.";
        return RedirectToAction(nameof(Details), new { id = sheet.Id });
    }

    // ------------------------------------------------------------------
    // Edit / manage (the teacher who uploaded the sheet)
    // ------------------------------------------------------------------
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var sheet = await _db.LearningActivitySheets.FirstOrDefaultAsync(x => x.Id == id);
        if (sheet is null) return NotFound();
        if (!CanEdit(sheet)) return Forbid();

        var model = new ActivitySheetFormViewModel
        {
            Id = sheet.Id,
            Title = sheet.Title,
            Description = sheet.Description,
            SubjectId = sheet.SubjectId,
            GradeLevelId = sheet.GradeLevelId,
            Term = sheet.Term,
            WeekNumber = sheet.WeekNumber,
            MelcCode = sheet.MelcCode,
            Competency = sheet.Competency,
            IsPublished = sheet.IsPublished,
            ExistingFileName = sheet.FileName,
            ExistingFileSize = sheet.FileSize
        };
        await PopulateOptionsAsync(model);

        ViewData["Title"] = $"Edit {sheet.Title}";
        ViewData["Section"] = "Academics";
        return View(model);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ActivitySheetFormViewModel model)
    {
        var sheet = await _db.LearningActivitySheets.FirstOrDefaultAsync(x => x.Id == id);
        if (sheet is null) return NotFound();
        if (!CanEdit(sheet)) return Forbid();

        ValidateFile(model.File, required: false);
        await ValidateSubjectAsync(model);

        if (!ModelState.IsValid)
        {
            model.ExistingFileName = sheet.FileName;
            model.ExistingFileSize = sheet.FileSize;
            await PopulateOptionsAsync(model);
            ViewData["Title"] = $"Edit {sheet.Title}";
            ViewData["Section"] = "Academics";
            return View(model);
        }

        sheet.Title = model.Title.Trim();
        sheet.Description = model.Description?.Trim();
        sheet.SubjectId = model.SubjectId;
        sheet.GradeLevelId = model.GradeLevelId is > 0 ? model.GradeLevelId : null;
        sheet.Term = model.Term;
        sheet.WeekNumber = model.WeekNumber;
        sheet.MelcCode = model.MelcCode?.Trim();
        sheet.Competency = model.Competency?.Trim();
        sheet.IsPublished = model.IsPublished;
        sheet.UpdatedAt = DateTime.UtcNow;

        // Replacing the file is optional: only overwrite when a new one is chosen.
        if (model.File is not null)
        {
            sheet.FileName = Path.GetFileName(model.File.FileName);
            sheet.ContentType = "application/pdf";
            sheet.FileSize = model.File.Length;
            sheet.FileContent = await ReadFileAsync(model.File);
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Activity Sheet", "LearningActivitySheet", sheet.Id, $"Updated \"{sheet.Title}\".");
        TempData["Success"] = "Activity sheet updated.";
        return RedirectToAction(nameof(Details), new { id = sheet.Id });
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var sheet = await _db.LearningActivitySheets.FirstOrDefaultAsync(x => x.Id == id);
        if (sheet is null) return NotFound();
        if (!CanEdit(sheet)) return Forbid();

        sheet.IsPublished = !sheet.IsPublished;
        sheet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            sheet.IsPublished ? "Publish Activity Sheet" : "Unpublish Activity Sheet",
            "LearningActivitySheet", sheet.Id, $"\"{sheet.Title}\".");
        TempData["Success"] = sheet.IsPublished ? "Activity sheet published." : "Activity sheet unpublished.";
        return RedirectToAction(nameof(Details), new { id = sheet.Id });
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var sheet = await _db.LearningActivitySheets.FirstOrDefaultAsync(x => x.Id == id);
        if (sheet is null) return NotFound();
        if (!CanEdit(sheet)) return Forbid();

        _db.LearningActivitySheets.Remove(sheet);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Activity Sheet", "LearningActivitySheet", id, $"Deleted \"{sheet.Title}\".");
        TempData["Success"] = "Activity sheet deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Only published sheets are public; teachers also see their own drafts.</summary>
    private IQueryable<LearningActivitySheet> ApplyVisibility(IQueryable<LearningActivitySheet> query)
    {
        if (CanUpload) return query.Where(x => x.IsPublished || x.UploadedByUserId == _currentUser.UserId);
        return query.Where(x => x.IsPublished);
    }

    /// <summary>Only the teacher who uploaded a sheet may edit, publish or delete it.</summary>
    private bool CanEdit(LearningActivitySheet sheet) =>
        CanUpload && sheet.UploadedByUserId == _currentUser.UserId;

    private void ValidateFile(IFormFile? file, bool required)
    {
        if (file is null)
        {
            if (required) ModelState.AddModelError(nameof(ActivitySheetFormViewModel.File), "Please choose a file to upload.");
            return;
        }

        if (file.Length == 0)
        {
            ModelState.AddModelError(nameof(ActivitySheetFormViewModel.File), "The selected file is empty.");
            return;
        }

        if (file.Length > MaxFileSize)
        {
            ModelState.AddModelError(nameof(ActivitySheetFormViewModel.File),
                $"The file is too large. The maximum size is {LearningActivitySheet.FileSizeLabelOf(MaxFileSize)}.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(ActivitySheetFormViewModel.File),
                "Only PDF files are allowed.");
        }
    }

    private async Task ValidateSubjectAsync(ActivitySheetFormViewModel model)
    {
        if (model.SubjectId > 0 && !await _db.Subjects.AnyAsync(s => s.Id == model.SubjectId))
        {
            ModelState.AddModelError(nameof(ActivitySheetFormViewModel.SubjectId), "Please choose a valid subject.");
        }
    }

    private async Task PopulateOptionsAsync(ActivitySheetFormViewModel model)
    {
        model.Subjects = await SubjectOptionsAsync();
        model.GradeLevels = await GradeLevelOptionsAsync();
    }

    private async Task<List<SelectListItem>> SubjectOptionsAsync() =>
        await _db.Subjects
            .OrderBy(s => s.Code)
            .Select(s => new SelectListItem($"{s.Code} - {s.Title}", s.Id.ToString()))
            .ToListAsync();

    private async Task<List<SelectListItem>> GradeLevelOptionsAsync() =>
        await _db.GradeLevels
            .OrderBy(g => g.OrderIndex)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString()))
            .ToListAsync();

    private static async Task<byte[]> ReadFileAsync(IFormFile file)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        return stream.ToArray();
    }
}
