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
public class AnnouncementsController : Controller
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public AnnouncementsController(
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
    // Read (everyone)
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index(AnnouncementPriority? priority, Audience? audience, string? keyword, int page = 1)
    {
        var audiences = VisibleAudiences();

        var query = _db.Announcements
            .Include(a => a.Author)
            .Where(a => audiences.Contains(a.Audience));

        if (priority.HasValue) query = query.Where(a => a.Priority == priority);
        if (audience.HasValue) query = query.Where(a => a.Audience == audience);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(a => a.Title.Contains(k) || a.Content.Contains(k) || a.AuthorName.Contains(k));
        }

        var paged = await PagedList<Announcement>.CreateAsync(
            query.OrderByDescending(a => a.PublishDate).ThenByDescending(a => a.Id), page, PageSize);

        var vm = new AnnouncementListViewModel
        {
            Announcements = paged,
            Priority = priority,
            Audience = audience,
            Keyword = keyword,
            CanManage = _currentUser.IsAdministrator
        };

        ViewData["Title"] = "Announcements";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "Announcements",
            RouteValues = new Dictionary<string, string?>
            {
                ["priority"] = priority?.ToString(),
                ["audience"] = audience?.ToString(),
                ["keyword"] = keyword
            }
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var announcement = await _db.Announcements
            .Include(a => a.Author)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (announcement is null) return NotFound();
        if (!VisibleAudiences().Contains(announcement.Audience)) return Forbid();

        ViewData["Title"] = announcement.Title;
        ViewData["CanManage"] = _currentUser.IsAdministrator;
        return View(announcement);
    }

    // ------------------------------------------------------------------
    // Create (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.CanPostAnnouncements)]
    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Announcement";
        return View(new Announcement { PublishDate = DateTime.Now, IsPublished = true });
    }

    [Authorize(Policy = Policies.CanPostAnnouncements)]
    [HttpPost]
    public async Task<IActionResult> Create(Announcement model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Announcement";
            return View(model);
        }

        model.AuthorUserId = _currentUser.UserId;
        model.AuthorName = _currentUser.FullName;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;

        _db.Announcements.Add(model);
        await _db.SaveChangesAsync();

        if (model.IsPublished) await NotifyAudienceAsync(model);

        await _audit.LogAsync("Create Announcement", "Announcement", model.Id, $"Posted \"{model.Title}\".");
        TempData["Success"] = "Announcement posted.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ------------------------------------------------------------------
    // Edit (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.CanPostAnnouncements)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement is null) return NotFound();

        ViewData["Title"] = $"Edit {announcement.Title}";
        return View(announcement);
    }

    [Authorize(Policy = Policies.CanPostAnnouncements)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Announcement model)
    {
        var announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Edit {announcement.Title}";
            return View(model);
        }

        announcement.Title = model.Title;
        announcement.Content = model.Content;
        announcement.Priority = model.Priority;
        announcement.Audience = model.Audience;
        announcement.PublishDate = model.PublishDate;
        announcement.ExpireDate = model.ExpireDate;
        announcement.IsPublished = model.IsPublished;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        if (announcement.IsPublished) await NotifyAudienceAsync(announcement);

        await _audit.LogAsync("Update Announcement", "Announcement", announcement.Id, $"Updated \"{announcement.Title}\".");
        TempData["Success"] = "Announcement updated.";
        return RedirectToAction(nameof(Details), new { id = announcement.Id });
    }

    // ------------------------------------------------------------------
    // Delete (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.CanPostAnnouncements)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var announcement = await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id);
        if (announcement is null) return NotFound();

        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Announcement", "Announcement", id, $"Deleted \"{announcement.Title}\".");
        TempData["Success"] = "Announcement deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private List<Audience> VisibleAudiences()
    {
        var list = new List<Audience> { Audience.All };

        if (_currentUser.IsAdministrator)
        {
            list.AddRange(new[]
            {
                Audience.Students, Audience.Teachers, Audience.Staff, Audience.Administrators
            });
        }
        else if (_currentUser.IsTeacher)
        {
            list.Add(Audience.Teachers);
        }
        else if (_currentUser.IsRegistrar || _currentUser.IsIctHeadCoordinator)
        {
            list.Add(Audience.Staff);
        }
        else if (_currentUser.IsStudent)
        {
            list.Add(Audience.Students);
        }

        return list;
    }

    private async Task NotifyAudienceAsync(Announcement announcement)
    {
        var url = Url.Action(nameof(Details), "Announcements", new { id = announcement.Id });
        var message = announcement.Content.Length > 200
            ? announcement.Content[..200] + "…"
            : announcement.Content;

        foreach (var role in RolesForAudience(announcement.Audience))
        {
            await _notify.NotifyRoleAsync(role, $"Announcement: {announcement.Title}", message, NotificationType.Info, url);
        }
    }

    private static IEnumerable<string> RolesForAudience(Audience audience) => audience switch
    {
        Audience.Students => new[] { Roles.Student },
        Audience.Teachers => new[] { Roles.Teacher },
        Audience.Staff => new[] { Roles.Registrar, Roles.IctHeadCoordinator },
        Audience.Administrators => Roles.Administrators,
        _ => new[] { Roles.Student, Roles.Teacher, Roles.Registrar, Roles.IctHeadCoordinator, Roles.SchoolAdministrator, Roles.SuperAdministrator }
    };
}
