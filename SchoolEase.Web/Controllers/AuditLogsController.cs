using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;

namespace SchoolEase.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class AuditLogsController : Controller
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? keyword, string? action, string? entityName, int page = 1)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entityName)) query = query.Where(a => a.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(a => a.UserName != null && a.UserName.Contains(k)
                || a.Details != null && a.Details.Contains(k)
                || a.Action.Contains(k)
                || a.IpAddress != null && a.IpAddress.Contains(k));
        }

        var paged = await PagedList<AuditLog>.CreateAsync(
            query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id), page, PageSize);

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var vm = new AuditLogIndexViewModel
        {
            Logs = paged,
            Keyword = keyword,
            Action = action,
            EntityName = entityName,
            Total = await _db.AuditLogs.CountAsync(),
            Today = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= today && a.CreatedAt < tomorrow),
            LoginsToday = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= today && a.CreatedAt < tomorrow && a.Action.Contains("Login")),
            DeletionsToday = await _db.AuditLogs.CountAsync(a => a.CreatedAt >= today && a.CreatedAt < tomorrow && a.Action.Contains("Delete")),
            Actions = await _db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(),
            Modules = await _db.AuditLogs
                .Where(a => a.EntityName != null)
                .Select(a => a.EntityName!)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync()
        };

        ViewData["Title"] = "Audit Logs";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "AuditLogs",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["action"] = action,
                ["entityName"] = entityName
            }
        };

        return View(vm);
    }
}
