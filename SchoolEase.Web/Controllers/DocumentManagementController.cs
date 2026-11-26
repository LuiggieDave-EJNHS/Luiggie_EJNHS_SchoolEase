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
/// Digital document management console. Staff browse every scanned record kept across
/// the school's 201 files, filter them down, then preview, download or remove each one.
/// Uploading still happens per learner from the student profile.
/// </summary>
[Authorize(Policy = Policies.StaffOnly)]
public class DocumentManagementController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IFileStorage _storage;

    public DocumentManagementController(
        ApplicationDbContext db,
        IAuditService audit,
        IFileStorage storage)
    {
        _db = db;
        _audit = audit;
        _storage = storage;
    }

    // ----------------------------------------------------------------- Index
    public async Task<IActionResult> Index(
        string? keyword,
        StudentDocumentCategory? category,
        int? gradeLevelId,
        bool? shared,
        int page = 1)
    {
        var query = _db.StudentDocuments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(d =>
                d.Title.Contains(term) ||
                d.FileName.Contains(term) ||
                d.Student!.FirstName.Contains(term) ||
                d.Student!.LastName.Contains(term) ||
                d.Student!.StudentNumber.Contains(term));
        }

        if (category is not null) query = query.Where(d => d.Category == category);
        if (gradeLevelId is > 0) query = query.Where(d => d.Student!.GradeLevelId == gradeLevelId);
        if (shared is not null) query = query.Where(d => d.IsVisibleToStudent == shared);

        var documents = await PagedList<DocumentRowViewModel>.CreateAsync(
            query.OrderByDescending(d => d.CreatedAt).Select(d => new DocumentRowViewModel
            {
                Id = d.Id,
                Title = d.Title,
                Category = d.Category,
                Description = d.Description,
                FileName = d.FileName,
                FileSize = d.FileSize,
                IsVisibleToStudent = d.IsVisibleToStudent,
                SchoolYear = d.SchoolYear,
                UploaderName = d.UploaderName,
                DownloadCount = d.DownloadCount,
                CreatedAt = d.CreatedAt,
                StudentId = d.StudentId,
                FirstName = d.Student!.FirstName,
                MiddleName = d.Student!.MiddleName,
                LastName = d.Student!.LastName,
                Suffix = d.Student!.Suffix,
                StudentNumber = d.Student!.StudentNumber,
                GradeLevelName = d.Student!.GradeLevel == null ? null : d.Student.GradeLevel.Name,
                SectionName = d.Student!.Section == null ? null : d.Student.Section.Name
            }),
            page,
            PageSize);

        var totalDocs = await _db.StudentDocuments.CountAsync();
        var learnersWithFiles = await _db.StudentDocuments.Select(d => d.StudentId).Distinct().CountAsync();
        var sharedDocs = await _db.StudentDocuments.CountAsync(d => d.IsVisibleToStudent);
        var thisYearDocs = await _db.StudentDocuments.CountAsync(d => d.SchoolYear == SchoolInfo.SchoolYear);
        var totalLearners = await _db.Students.CountAsync();
        var storageBytes = await _db.StudentDocuments.SumAsync(d => (long?)d.FileSize) ?? 0;

        var vm = new DocumentManagementIndexViewModel
        {
            Documents = documents,
            Keyword = keyword,
            Category = category,
            GradeLevelId = gradeLevelId,
            Shared = shared,
            Categories = SelectLists.EnumOptions<StudentDocumentCategory>(category),
            GradeLevels = await BuildGradeLevelOptionsAsync(gradeLevelId),
            Stats = new List<StatCard>
            {
                new() { Label = "Documents on File", Value = totalDocs.ToString("N0"), Icon = "bi-folder2-open", Tone = "primary", Meta = $"{thisYearDocs:N0} this school year" },
                new() { Label = "Learners with Records", Value = learnersWithFiles.ToString("N0"), Icon = "bi-people", Tone = "info", Meta = $"of {totalLearners:N0} learners" },
                new() { Label = "Shared with Learners", Value = sharedDocs.ToString("N0"), Icon = "bi-eye", Tone = "success", Meta = $"{totalDocs - sharedDocs:N0} staff-only" },
                new() { Label = "Storage Used", Value = LearningActivitySheet.FileSizeLabelOf(storageBytes), Icon = "bi-hdd", Tone = "warning", Meta = _storage.IsConfigured ? "Cloudflare R2" : "Database storage" }
            }
        };

        ViewData["Title"] = "201 File Console";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = documents.PageIndex,
            TotalPages = documents.TotalPages,
            TotalCount = documents.TotalCount,
            PageSize = documents.PageSize,
            Action = "Index",
            Controller = "DocumentManagement",
            NoResultsText = "No documents match your filters.",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["category"] = category?.ToString(),
                ["gradeLevelId"] = gradeLevelId?.ToString(),
                ["shared"] = shared?.ToString()
            }
        };

        return View(vm);
    }

    // ---------------------------------------------------------------- Delete
    [HttpPost]
    public async Task<IActionResult> Delete(
        int id,
        string? keyword,
        StudentDocumentCategory? category,
        int? gradeLevelId,
        bool? shared,
        int page = 1)
    {
        var document = await _db.StudentDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return NotFound();

        var storageKey = document.StorageKey;
        _db.StudentDocuments.Remove(document);
        await _db.SaveChangesAsync();

        if (storageKey is not null)
        {
            await _storage.DeleteAsync(storageKey);
        }

        await _audit.LogAsync("Delete Student Document", "StudentDocument", id, $"Deleted \"{document.Title}\".");
        TempData["Success"] = "Document removed from the 201 file.";
        return RedirectToAction(nameof(Index), new { keyword, category, gradeLevelId, shared, page });
    }

    // --------------------------------------------------------------- Helpers
    private async Task<List<SelectListItem>> BuildGradeLevelOptionsAsync(int? selected)
    {
        var levels = await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync();
        return levels.Select(g => new SelectListItem(g.Name, g.Id.ToString(), selected == g.Id)).ToList();
    }
}
