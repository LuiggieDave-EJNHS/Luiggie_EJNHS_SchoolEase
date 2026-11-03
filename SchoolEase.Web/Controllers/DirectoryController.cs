using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Controllers;

/// <summary>Inter-school directory for registrars.</summary>
[Authorize(Roles = Roles.Registrar)]
public class DirectoryController : Controller
{
    private readonly ApplicationDbContext _db;

    public DirectoryController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.SchoolContacts.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Name.Contains(s) || (x.SchoolId != null && x.SchoolId.Contains(s)) || (x.Division != null && x.Division.Contains(s)));
        }

        var contacts = await query.OrderBy(s => s.Name).ToListAsync();
        ViewData["Search"] = search;
        return View(contacts);
    }
}
