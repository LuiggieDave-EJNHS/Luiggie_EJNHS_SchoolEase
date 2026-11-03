using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Services;

/// <summary>Writes tamper-evident entries to the audit trail.</summary>
public interface IAuditService
{
    Task LogAsync(string action, string? entityName = null, object? entityId = null, string? details = null, string? userName = null, string? userId = null);
    Task<List<AuditLog>> RecentAsync(int count = 10);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _accessor;

    public AuditService(ApplicationDbContext db, ICurrentUserService currentUser, IHttpContextAccessor accessor)
    {
        _db = db;
        _currentUser = currentUser;
        _accessor = accessor;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        object? entityId = null,
        string? details = null,
        string? userName = null,
        string? userId = null)
    {
        var log = new AuditLog
        {
            Action = Truncate(action, 60)!,
            EntityName = Truncate(entityName, 60),
            EntityId = Truncate(entityId?.ToString(), 60),
            Details = Truncate(details, 1000),
            UserId = userId ?? _currentUser.UserId,
            UserName = Truncate(userName ?? _currentUser.FullName, 120),
            IpAddress = Truncate(_accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), 60),
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> RecentAsync(int count = 10) =>
        await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
