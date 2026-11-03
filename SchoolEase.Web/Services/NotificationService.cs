using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Services;

/// <summary>Creates in-app notifications for individual users or whole roles.</summary>
public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string message, NotificationType type = NotificationType.Info, string? url = null);
    Task NotifyRoleAsync(string role, string title, string message, NotificationType type = NotificationType.Info, string? url = null);
    Task NotifyAdministratorsAsync(string title, string message, NotificationType type = NotificationType.Info, string? url = null);
    Task<int> UnreadCountAsync(string userId);
    Task<List<Notification>> RecentAsync(string userId, int count = 8);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public NotificationService(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task NotifyAsync(string userId, string title, string message, NotificationType type = NotificationType.Info, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Url = url,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task NotifyRoleAsync(string role, string title, string message, NotificationType type = NotificationType.Info, string? url = null)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);
        if (users.Count == 0) return;

        foreach (var user in users.Where(u => u.IsActive))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task NotifyAdministratorsAsync(string title, string message, NotificationType type = NotificationType.Info, string? url = null)
    {
        await NotifyRoleAsync(Roles.SchoolAdministrator, title, message, type, url);
        await NotifyRoleAsync(Roles.SuperAdministrator, title, message, type, url);
    }

    public Task<int> UnreadCountAsync(string userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public Task<List<Notification>> RecentAsync(string userId, int count = 8) =>
        _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync();
}
