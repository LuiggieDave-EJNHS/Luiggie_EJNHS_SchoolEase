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

[Authorize]
public class DocumentRequestsController : Controller
{
    private const int PageSize = 15;

    private static readonly string[] Palette =
    {
        "#f2a93b", "#17a2b8", "#2f6fed", "#1aa179", "#dc3545"
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public DocumentRequestsController(
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

    // ------------------------------------------------------------------
    // Registrar worklist
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index(DocumentRequestStatus? status, string? keyword, int page = 1)
    {
        if (_currentUser.IsStudent) return RedirectToAction(nameof(MyRequests));

        var query = _db.DocumentRequests
            .Include(r => r.Student).ThenInclude(s => s.Section)
            .Include(r => r.DocumentType)
            .Include(r => r.ProcessedBy)
            .AsQueryable();

        if (status.HasValue) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(r => r.RequestNumber.Contains(k)
                || r.Purpose.Contains(k)
                || r.Student.FirstName.Contains(k)
                || r.Student.LastName.Contains(k)
                || r.DocumentType.Name.Contains(k));
        }

        var paged = await PagedList<DocumentRequest>.CreateAsync(
            query.OrderByDescending(r => r.RequestedAt), page, PageSize);

        var counts = await _db.DocumentRequests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var statuses = new[]
        {
            DocumentRequestStatus.Pending, DocumentRequestStatus.Processing,
            DocumentRequestStatus.ReadyForRelease, DocumentRequestStatus.Released,
            DocumentRequestStatus.Rejected
        };

        var vm = new DocumentRequestListViewModel
        {
            Requests = paged,
            Status = status,
            Keyword = keyword,
            Total = counts.Sum(c => c.Count),
            Pending = counts.FirstOrDefault(c => c.Status == DocumentRequestStatus.Pending)?.Count ?? 0,
            Processing = counts.FirstOrDefault(c => c.Status == DocumentRequestStatus.Processing)?.Count ?? 0,
            ReadyForRelease = counts.FirstOrDefault(c => c.Status == DocumentRequestStatus.ReadyForRelease)?.Count ?? 0,
            Released = counts.FirstOrDefault(c => c.Status == DocumentRequestStatus.Released)?.Count ?? 0,
            IsStaffView = _currentUser.IsAdministrator || _currentUser.IsRegistrar,
            StatusChart = new ChartData
            {
                Title = "Requests by Status",
                Labels = statuses.Select(s => s.DisplayName()).ToList(),
                Values = statuses.Select(s => (double)(counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0)).ToList(),
                Colors = Palette.ToList()
            }
        };

        ViewData["Title"] = "Document Requests";
        ViewData["Pagination"] = BuildPagination(paged, "DocumentRequests", new Dictionary<string, string?>
        {
            ["status"] = status?.ToString(),
            ["keyword"] = keyword
        });

        return View(vm);
    }

    /// <summary>Student-facing list restricted to the signed-in learner's own requests.</summary>
    public async Task<IActionResult> MyRequests(int page = 1)
    {
        var student = await GetCurrentStudentAsync();

        var vm = new DocumentRequestListViewModel { IsStaffView = false };

        if (student is null)
        {
            TempData["Warning"] = "Your account is not linked to a student record. Please contact the registrar.";
            ViewData["Title"] = "My Document Requests";
            return View(vm);
        }

        var query = _db.DocumentRequests
            .Include(r => r.DocumentType)
            .Include(r => r.ProcessedBy)
            .Where(r => r.StudentId == student.Id);

        var paged = await PagedList<DocumentRequest>.CreateAsync(
            query.OrderByDescending(r => r.RequestedAt), page, PageSize);

        vm.Requests = paged;
        ViewData["Title"] = "My Document Requests";
        ViewData["Pagination"] = BuildPagination(paged, "DocumentRequests");
        return View(vm);
    }

    // ------------------------------------------------------------------
    // Details
    // ------------------------------------------------------------------
    public async Task<IActionResult> Details(int id)
    {
        var request = await _db.DocumentRequests
            .Include(r => r.Student).ThenInclude(s => s.Section)
            .Include(r => r.Student).ThenInclude(s => s.GradeLevel)
            .Include(r => r.DocumentType)
            .Include(r => r.ProcessedBy)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null) return NotFound();

        if (_currentUser.IsStudent && request.Student.UserId != _currentUser.UserId) return Forbid();

        var vm = new DocumentRequestDetailsViewModel
        {
            Request = request,
            CanManage = _currentUser.IsAdministrator || _currentUser.IsRegistrar,
            Timeline = BuildTimeline(request)
        };

        ViewData["Title"] = $"Request {request.RequestNumber}";
        return View(vm);
    }

    // ------------------------------------------------------------------
    // Create (students only)
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!_currentUser.IsStudent)
        {
            TempData["Error"] = "Only student accounts can request documents.";
            return RedirectToAction(nameof(Index));
        }

        if (await GetCurrentStudentAsync() is null)
        {
            TempData["Warning"] = "Your account is not linked to a student record. Please contact the registrar.";
            return RedirectToAction(nameof(MyRequests));
        }

        ViewData["Title"] = "New Document Request";
        return View(new DocumentRequestCreateViewModel
        {
            DocumentTypes = await _db.DocumentTypes
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(int documentTypeId, string purpose, int copies)
    {
        if (!_currentUser.IsStudent)
        {
            TempData["Error"] = "Only student accounts can request documents.";
            return RedirectToAction(nameof(Index));
        }

        var student = await GetCurrentStudentAsync();
        if (student is null)
        {
            TempData["Error"] = "Your account is not linked to a student record.";
            return RedirectToAction(nameof(MyRequests));
        }

        var type = await _db.DocumentTypes.FirstOrDefaultAsync(d => d.Id == documentTypeId && d.IsActive);
        if (type is null) ModelState.AddModelError(nameof(documentTypeId), "Please choose a valid document type.");
        if (string.IsNullOrWhiteSpace(purpose)) ModelState.AddModelError(nameof(purpose), "Please state the purpose of your request.");
        if (copies < 1 || copies > 20) ModelState.AddModelError(nameof(copies), "Number of copies must be between 1 and 20.");

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Document Request";
            return View(new DocumentRequestCreateViewModel
            {
                DocumentTypes = await _db.DocumentTypes.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(),
                DocumentTypeId = documentTypeId,
                Purpose = purpose,
                Copies = copies
            });
        }

        var now = DateTime.UtcNow;
        var request = new DocumentRequest
        {
            RequestNumber = await GenerateRequestNumberAsync(),
            StudentId = student.Id,
            DocumentTypeId = documentTypeId,
            Purpose = purpose.Trim(),
            Copies = copies,
            Status = DocumentRequestStatus.Pending,
            RequestedAt = now,
            CreatedAt = now
        };

        _db.DocumentRequests.Add(request);
        await _db.SaveChangesAsync();

        await _notify.NotifyAdministratorsAsync(
            "New document request",
            $"{student.FullName} requested {copies}× {type!.Name} ({request.RequestNumber}).",
            NotificationType.Info,
            Url.Action(nameof(Details), "DocumentRequests", new { id = request.Id }));

        await _audit.LogAsync("Create Document Request", "DocumentRequest", request.Id,
            $"Request {request.RequestNumber} submitted for {student.FullName}.");

        TempData["Success"] = $"Request {request.RequestNumber} submitted. We will notify you once it is ready.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    // ------------------------------------------------------------------
    // Update status (staff / admins only)
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, DocumentRequestStatus status, string? remarks)
    {
        if (!_currentUser.IsAdministrator && !_currentUser.IsRegistrar)
        {
            TempData["Error"] = "You are not allowed to process document requests.";
            return RedirectToAction(nameof(Index));
        }

        var request = await _db.DocumentRequests
            .Include(r => r.Student)
            .Include(r => r.DocumentType)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request is null) return NotFound();

        request.Status = status;
        request.Remarks = remarks;
        request.ProcessedByUserId = _currentUser.UserId;
        request.ProcessedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        if (status == DocumentRequestStatus.Released) request.ReleasedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(request.Student?.UserId))
        {
            await _notify.NotifyAsync(
                request.Student.UserId,
                $"Request {request.RequestNumber} — {status.DisplayName()}",
                $"Your request for {request.DocumentType.Name} is now {status.DisplayName()}.",
                status == DocumentRequestStatus.Released ? NotificationType.Success : NotificationType.Info,
                Url.Action(nameof(Details), "DocumentRequests", new { id = request.Id }));
        }

        await _audit.LogAsync("Update Request Status", "DocumentRequest", request.Id,
            $"Request {request.RequestNumber} set to {status.DisplayName()}.");

        TempData["Success"] = "Request status updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------
    // Delete (admins only)
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!_currentUser.IsAdministrator)
        {
            TempData["Error"] = "Only administrators can delete requests.";
            return RedirectToAction(nameof(Index));
        }

        var request = await _db.DocumentRequests.FirstOrDefaultAsync(r => r.Id == id);
        if (request is null) return NotFound();

        _db.DocumentRequests.Remove(request);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Document Request", "DocumentRequest", id, $"Removed request {request.RequestNumber}.");

        TempData["Success"] = "Document request deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Document type management
    // ------------------------------------------------------------------
    public async Task<IActionResult> Types()
    {
        if (!_currentUser.IsAdministrator && !_currentUser.IsRegistrar)
        {
            return RedirectToAction(nameof(Index));
        }

        var types = await _db.DocumentTypes.OrderBy(d => d.Name).ToListAsync();
        var counts = await _db.DocumentRequests
            .GroupBy(r => r.DocumentTypeId)
            .Select(g => new { DocumentTypeId = g.Key, Count = g.Count() })
            .ToListAsync();

        var vm = new DocumentTypesViewModel
        {
            Types = types.Select(t => new DocumentTypeRow
            {
                Type = t,
                RequestCount = counts.FirstOrDefault(c => c.DocumentTypeId == t.Id)?.Count ?? 0
            }).ToList()
        };

        ViewData["Title"] = "Document Types";
        return View(vm);
    }

    [HttpGet]
    public IActionResult CreateType()
    {
        if (!_currentUser.IsAdministrator) return Forbid();
        ViewData["Title"] = "New Document Type";
        return View(new DocumentType { ProcessingDays = 3, IsActive = true });
    }

    [HttpPost]
    public async Task<IActionResult> CreateType(DocumentType model)
    {
        if (!_currentUser.IsAdministrator) return Forbid();

        if (await _db.DocumentTypes.AnyAsync(d => d.Code == model.Code.Trim()))
        {
            ModelState.AddModelError(nameof(model.Code), "A document type with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Document Type";
            return View(model);
        }

        model.Code = model.Code.Trim();
        model.Name = model.Name.Trim();
        _db.DocumentTypes.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Document Type", "DocumentType", model.Id, $"Added document type {model.Name}.");
        TempData["Success"] = "Document type added.";
        return RedirectToAction(nameof(Types));
    }

    [HttpGet]
    public async Task<IActionResult> EditType(int id)
    {
        if (!_currentUser.IsAdministrator) return Forbid();

        var type = await _db.DocumentTypes.FirstOrDefaultAsync(d => d.Id == id);
        if (type is null) return NotFound();

        ViewData["Title"] = $"Edit {type.Name}";
        return View(type);
    }

    [HttpPost]
    public async Task<IActionResult> EditType(DocumentType model)
    {
        if (!_currentUser.IsAdministrator) return Forbid();

        var type = await _db.DocumentTypes.FirstOrDefaultAsync(d => d.Id == model.Id);
        if (type is null) return NotFound();

        if (await _db.DocumentTypes.AnyAsync(d => d.Code == model.Code.Trim() && d.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Code), "A document type with this code already exists.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Edit {type.Name}";
            return View(model);
        }

        type.Name = model.Name.Trim();
        type.Code = model.Code.Trim();
        type.Description = model.Description;
        type.ProcessingDays = model.ProcessingDays;
        type.IsActive = model.IsActive;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Document Type", "DocumentType", type.Id, $"Updated document type {type.Name}.");
        TempData["Success"] = "Document type updated.";
        return RedirectToAction(nameof(Types));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteType(int id)
    {
        if (!_currentUser.IsAdministrator) return Forbid();

        var type = await _db.DocumentTypes.FirstOrDefaultAsync(d => d.Id == id);
        if (type is null) return NotFound();

        if (await _db.DocumentRequests.AnyAsync(r => r.DocumentTypeId == id))
        {
            TempData["Error"] = $"\"{type.Name}\" cannot be deleted because it is used by existing requests.";
            return RedirectToAction(nameof(Types));
        }

        _db.DocumentTypes.Remove(type);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Document Type", "DocumentType", id, $"Removed document type {type.Name}.");
        TempData["Success"] = "Document type deleted.";
        return RedirectToAction(nameof(Types));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private Task<Student?> GetCurrentStudentAsync() =>
        _db.Students.FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId);

    private async Task<string> GenerateRequestNumberAsync()
    {
        var prefix = $"DR-{DateTime.Now.Year}-";
        var numbers = await _db.DocumentRequests
            .Where(r => r.RequestNumber.StartsWith(prefix))
            .Select(r => r.RequestNumber)
            .ToListAsync();

        var next = numbers
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefix}{next:D4}";
    }

    private static List<TimelineStep> BuildTimeline(DocumentRequest request)
    {
        var rejected = request.Status == DocumentRequestStatus.Rejected;
        var reachedProcessing = request.Status is DocumentRequestStatus.Processing
            or DocumentRequestStatus.ReadyForRelease or DocumentRequestStatus.Released;
        var reachedReady = request.Status is DocumentRequestStatus.ReadyForRelease or DocumentRequestStatus.Released;
        var released = request.Status == DocumentRequestStatus.Released;

        return new List<TimelineStep>
        {
            new()
            {
                Label = "Requested",
                Detail = "Request received by the registrar.",
                Timestamp = request.RequestedAt,
                IsComplete = true,
                IsCurrent = request.Status == DocumentRequestStatus.Pending
            },
            new()
            {
                Label = "Processing",
                Detail = "The registrar is preparing the document.",
                Timestamp = reachedProcessing ? request.ProcessedAt : null,
                IsComplete = reachedProcessing,
                IsCurrent = request.Status == DocumentRequestStatus.Processing
            },
            new()
            {
                Label = "Ready for Release",
                Detail = "Document is ready for pickup.",
                Timestamp = reachedReady ? request.ProcessedAt : null,
                IsComplete = reachedReady,
                IsCurrent = request.Status == DocumentRequestStatus.ReadyForRelease
            },
            new()
            {
                Label = "Released",
                Detail = "Document handed to the student.",
                Timestamp = released ? request.ReleasedAt : null,
                IsComplete = released,
                IsCurrent = released
            },
            new()
            {
                Label = "Rejected",
                Detail = "Request was declined.",
                Timestamp = rejected ? request.ProcessedAt : null,
                IsComplete = rejected,
                IsCurrent = rejected
            }
        }.Where(step => step.Label != "Rejected" || rejected).ToList();
    }

    private static PaginationInfo BuildPagination<T>(
        PagedList<T> paged,
        string controller,
        Dictionary<string, string?>? filters = null) =>
        new()
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = controller,
            RouteValues = filters ?? new Dictionary<string, string?>()
        };
}
