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

/// <summary>Parents and guardians linked to learners.</summary>
[Authorize(Policy = Policies.AdminOnly)]
public class GuardiansController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public GuardiansController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ----------------------------------------------------------------- Index
    public async Task<IActionResult> Index(string? keyword, string? sort, int page = 1)
    {
        var query = _db.Guardians.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(g =>
                g.FirstName.Contains(term) ||
                g.LastName.Contains(term) ||
                g.Relationship.Contains(term) ||
                (g.Email != null && g.Email.Contains(term)) ||
                (g.ContactNumber != null && g.ContactNumber.Contains(term)));
        }

        query = sort switch
        {
            "name" => query.OrderBy(g => g.FirstName).ThenBy(g => g.LastName),
            "-name" => query.OrderByDescending(g => g.FirstName).ThenByDescending(g => g.LastName),
            "relationship" => query.OrderBy(g => g.Relationship),
            _ => query.OrderBy(g => g.LastName).ThenBy(g => g.FirstName)
        };

        query = query.Include(g => g.Students);

        var guardians = await PagedList<Guardian>.CreateAsync(query, page, PageSize);

        var vm = new GuardianIndexViewModel
        {
            Guardians = guardians,
            Keyword = keyword,
            Sort = sort,
            Stats = new List<StatCard>
            {
                new() { Label = "Total Guardians", Value = (await _db.Guardians.CountAsync()).ToString("N0"), Icon = "bi-person-hearts", Tone = "pink", Meta = "Parents & guardians on file" },
                new() { Label = "Linked Students", Value = (await _db.Students.CountAsync(s => s.GuardianId != null)).ToString("N0"), Icon = "bi-people", Tone = "primary", Meta = "Students with a guardian" },
                new() { Label = "Without Guardian", Value = (await _db.Students.CountAsync(s => s.GuardianId == null)).ToString("N0"), Icon = "bi-person-dash", Tone = "warning", Meta = "Students missing a guardian" }
            }
        };

        ViewData["Title"] = "Guardians";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = guardians.PageIndex,
            TotalPages = guardians.TotalPages,
            TotalCount = guardians.TotalCount,
            PageSize = guardians.PageSize,
            Action = "Index",
            Controller = "Guardians",
            NoResultsText = "No guardians match your search.",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["sort"] = sort
            }
        };

        return View(vm);
    }

    // --------------------------------------------------------------- Details
    public async Task<IActionResult> Details(int id)
    {
        var guardian = await _db.Guardians
            .Include(g => g.Students)
                .ThenInclude(s => s.GradeLevel)
            .Include(g => g.Students)
                .ThenInclude(s => s.Section)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (guardian is null) return NotFound();

        ViewData["Title"] = guardian.FullName;
        return View(guardian);
    }

    // ---------------------------------------------------------------- Create
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Guardian";
        return View(new Guardian());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guardian model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Guardian";
            return View(model);
        }

        model.CreatedAt = DateTime.UtcNow;
        _db.Guardians.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Guardian", "Guardian", model.Id, model.FullName);
        TempData["Success"] = $"Guardian {model.FullName} was added.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ------------------------------------------------------------------ Edit
    public async Task<IActionResult> Edit(int id)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == id);
        if (guardian is null) return NotFound();

        ViewData["Title"] = $"Edit {guardian.FullName}";
        return View(guardian);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Guardian model)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == id);
        if (guardian is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Id = id;
            ViewData["Title"] = $"Edit {guardian.FullName}";
            return View(model);
        }

        guardian.FirstName = model.FirstName;
        guardian.MiddleName = model.MiddleName;
        guardian.LastName = model.LastName;
        guardian.Relationship = model.Relationship;
        guardian.ContactNumber = model.ContactNumber;
        guardian.Email = model.Email;
        guardian.Address = model.Address;
        guardian.Occupation = model.Occupation;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Guardian", "Guardian", guardian.Id, guardian.FullName);
        TempData["Success"] = $"Guardian {guardian.FullName} was updated.";
        return RedirectToAction(nameof(Details), new { id = guardian.Id });
    }

    // ---------------------------------------------------------------- Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == id);
        if (guardian is null) return NotFound();

        var name = guardian.FullName;
        var linked = await _db.Students.CountAsync(s => s.GuardianId == id);

        // The Student -> Guardian FK is configured with OnDelete(SetNull): deleting the
        // guardian leaves every linked learner in place with no guardian assigned.
        _db.Guardians.Remove(guardian);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Guardian", "Guardian", id, name);
        TempData["Success"] = linked == 0
            ? $"Guardian {name} was deleted."
            : $"Guardian {name} was deleted. {linked} linked student(s) kept their records.";
        return RedirectToAction(nameof(Index));
    }
}
