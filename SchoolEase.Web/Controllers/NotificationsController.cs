using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? filter)
    {
        var uid = _currentUser.UserId;
        if (uid is null) return Forbid();

        var query = _db.Notifications.Where(n => n.UserId == uid);
        if (string.Equals(filter, "unread", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query.OrderByDescending(n => n.CreatedAt).Take(200).ToListAsync();

        ViewData["Title"] = "Notifications";
        ViewData["Filter"] = filter;
        ViewData["Unread"] = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead);

        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(bool? redirect)
    {
        var uid = _currentUser.UserId;
        if (uid is null) return Forbid();

        var unread = await _db.Notifications.Where(n => n.UserId == uid && !n.IsRead).ToListAsync();
        foreach (var item in unread) item.IsRead = true;
        await _db.SaveChangesAsync();

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { ok = true, updated = unread.Count });
        }

        TempData["Success"] = unread.Count == 0
            ? "You had no unread notifications."
            : $"{unread.Count} notification(s) marked as read.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var uid = _currentUser.UserId;
        var item = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid);
        if (item is null) return NotFound();

        item.IsRead = true;
        await _db.SaveChangesAsync();

        return string.IsNullOrWhiteSpace(item.Url)
            ? RedirectToAction(nameof(Index))
            : Redirect(item.Url);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = _currentUser.UserId;
        var item = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == uid);
        if (item is null) return NotFound();

        _db.Notifications.Remove(item);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Notification removed.";
        return RedirectToAction(nameof(Index));
    }
}
