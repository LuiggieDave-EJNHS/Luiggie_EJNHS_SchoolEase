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

/// <summary>
/// Digital 201 files. Staff upload scanned learner records; the learner can then
/// read and download the records shared with them from their own portal.
/// </summary>
[Authorize(Policy = Policies.StaffOnly)]
public class StudentDocumentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly IFileStorage _storage;

    public StudentDocumentsController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        INotificationService notify,
        IFileStorage storage)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
        _storage = storage;
    }

    // ------------------------------------------------------------------ Upload
    [HttpGet]
    public async Task<IActionResult> Create(int studentId)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return NotFound();

        ViewData["Title"] = "Upload document";
        return View(new StudentDocumentFormViewModel
        {
            StudentId = student.Id,
            StudentLabel = $"{student.FullName} • {student.StudentNumber}"
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(StudentDocumentFormViewModel model)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == model.StudentId);
        if (student is null) return NotFound();

        model.StudentLabel = $"{student.FullName} • {student.StudentNumber}";
        var upload = model.File ?? model.Camera;
        DocumentUpload.Validate(upload, ModelState, nameof(model.File), required: true);

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Upload document";
            return View(model);
        }

        var document = new StudentDocument
        {
            StudentId = student.Id,
            Title = model.Title.Trim(),
            Category = model.Category,
            Description = model.Description?.Trim(),
            IsVisibleToStudent = model.IsVisibleToStudent,
            SchoolYear = SchoolInfo.SchoolYear,
            UploaderName = _currentUser.FullName,
            UploadedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await DocumentUpload.StoreAsync(document, upload!, _storage);

        _db.StudentDocuments.Add(document);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Upload Student Document", "StudentDocument", document.Id,
            $"{document.Title} ({document.Category.DisplayName()}) for {student.FullName}.");

        if (document.IsVisibleToStudent && !string.IsNullOrWhiteSpace(student.UserId))
        {
            await _notify.NotifyAsync(student.UserId,
                "New record in your 201 file",
                $"{document.Title} was added to your records.",
                NotificationType.Info,
                Url.Action("Documents", "Student"));
        }

        TempData["Success"] = "Document saved to the learner's 201 file.";
        return RedirectToAction("Details", "Students", new { id = student.Id });
    }

    // -------------------------------------------------------- Read / download
    public async Task<IActionResult> Download(int id)
    {
        var document = await _db.StudentDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return NotFound();

        var (bytes, contentType) = await ReadFileAsync(document);
        if (bytes.Length == 0) return NotFound();

        document.DownloadCount++;
        await _db.SaveChangesAsync();

        return File(bytes, contentType, document.FileName);
    }

    /// <summary>Streams the record inline. Framing is allowed same-origin only for this response.</summary>
    public async Task<IActionResult> Preview(int id)
    {
        var document = await _db.StudentDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return NotFound();

        var (bytes, contentType) = await ReadFileAsync(document);
        if (bytes.Length == 0) return NotFound();

        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self';";

        return File(bytes, contentType, enableRangeProcessing: true);
    }

    // ------------------------------------------------------------------ Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _db.StudentDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document is null) return NotFound();

        var studentId = document.StudentId;
        var storageKey = document.StorageKey;

        _db.StudentDocuments.Remove(document);
        await _db.SaveChangesAsync();

        if (storageKey is not null)
        {
            await _storage.DeleteAsync(storageKey);
        }

        await _audit.LogAsync("Delete Student Document", "StudentDocument", id, $"Deleted \"{document.Title}\".");
        TempData["Success"] = "Document removed from the 201 file.";
        return RedirectToAction("Details", "Students", new { id = studentId });
    }

    // ----------------------------------------------------------------- Helpers
    private async Task<(byte[] Bytes, string ContentType)> ReadFileAsync(StudentDocument document)
    {
        var bytes = document.StorageKey is not null && _storage.IsConfigured
            ? await _storage.DownloadAsync(document.StorageKey)
            : document.FileContent;

        return (bytes, document.ContentType);
    }
}
