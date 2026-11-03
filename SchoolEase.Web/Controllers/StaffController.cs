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

/// <summary>Non-teaching personnel register.</summary>
[Authorize(Policy = Policies.AdminOnly)]
public class StaffController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public StaffController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ----------------------------------------------------------------- Index
    public async Task<IActionResult> Index(string? keyword, string? department, string? sort, int page = 1)
    {
        var query = _db.StaffMembers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(s =>
                s.FirstName.Contains(term) ||
                s.LastName.Contains(term) ||
                s.StaffNumber.Contains(term) ||
                (s.Email != null && s.Email.Contains(term)) ||
                s.Position.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(s => s.Department == department);

        query = sort switch
        {
            "number" => query.OrderBy(s => s.StaffNumber),
            "-number" => query.OrderByDescending(s => s.StaffNumber),
            "-name" => query.OrderByDescending(s => s.LastName).ThenByDescending(s => s.FirstName),
            "department" => query.OrderBy(s => s.Department).ThenBy(s => s.LastName),
            _ => query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
        };

        var staff = await PagedList<Staff>.CreateAsync(query, page, PageSize);

        var vm = new StaffIndexViewModel
        {
            Staff = staff,
            Keyword = keyword,
            Department = department,
            Sort = sort,
            Departments = await BuildDepartmentOptionsAsync(department),
            Stats = new List<StatCard>
            {
                new() { Label = "Total Staff", Value = (await _db.StaffMembers.CountAsync()).ToString("N0"), Icon = "bi-person-workspace", Tone = "purple", Meta = $"{await _db.StaffMembers.CountAsync(s => s.IsActive)} active" },
                new() { Label = "Permanent", Value = (await _db.StaffMembers.CountAsync(s => s.EmploymentStatus == EmploymentStatus.Permanent)).ToString("N0"), Icon = "bi-patch-check", Tone = "success" },
                new() { Label = "Contractual / Part-Time", Value = (await _db.StaffMembers.CountAsync(s => s.EmploymentStatus == EmploymentStatus.Contractual || s.EmploymentStatus == EmploymentStatus.PartTime)).ToString("N0"), Icon = "bi-clock-history", Tone = "warning" },
                new() { Label = "Offices", Value = (await _db.StaffMembers.Where(s => s.Department != null && s.Department != "").Select(s => s.Department!).Distinct().CountAsync()).ToString("N0"), Icon = "bi-building", Tone = "teal" }
            }
        };

        ViewData["Title"] = "Staff";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = staff.PageIndex,
            TotalPages = staff.TotalPages,
            TotalCount = staff.TotalCount,
            PageSize = staff.PageSize,
            Action = "Index",
            Controller = "Staff",
            NoResultsText = "No staff members match your filters.",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["department"] = department,
                ["sort"] = sort
            }
        };

        return View(vm);
    }

    // --------------------------------------------------------------- Details
    public async Task<IActionResult> Details(int id)
    {
        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null) return NotFound();

        ViewData["Title"] = staff.FullName;
        return View(staff);
    }

    // ---------------------------------------------------------------- Create
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Staff";
        return View(new Staff
        {
            DateHired = DateTime.Today,
            EmploymentStatus = EmploymentStatus.Permanent,
            IsActive = true
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Staff model)
    {
        if (string.IsNullOrWhiteSpace(model.StaffNumber))
        {
            model.StaffNumber = await GenerateStaffNumberAsync();
        }
        else if (await _db.StaffMembers.AnyAsync(s => s.StaffNumber == model.StaffNumber))
        {
            ModelState.AddModelError(nameof(Staff.StaffNumber), $"Staff ID {model.StaffNumber} is already in use.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add Staff";
            return View(model);
        }

        model.IsActive = true;
        model.CreatedAt = DateTime.UtcNow;
        model.UpdatedAt = null;
        if (model.DateHired == default(DateTime)) model.DateHired = DateTime.Today;

        _db.StaffMembers.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Staff", "Staff", model.Id, model.FullName);
        TempData["Success"] = $"Staff member {model.FullName} was added.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    // ------------------------------------------------------------------ Edit
    public async Task<IActionResult> Edit(int id)
    {
        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null) return NotFound();

        ViewData["Title"] = $"Edit {staff.FullName}";
        return View(staff);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Staff model)
    {
        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.StaffNumber)) model.StaffNumber = staff.StaffNumber;

        if (await _db.StaffMembers.AnyAsync(s => s.StaffNumber == model.StaffNumber && s.Id != id))
        {
            ModelState.AddModelError(nameof(Staff.StaffNumber), $"Staff ID {model.StaffNumber} is already in use.");
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            ViewData["Title"] = $"Edit {staff.FullName}";
            return View(model);
        }

        staff.StaffNumber = model.StaffNumber;
        staff.FirstName = model.FirstName;
        staff.MiddleName = model.MiddleName;
        staff.LastName = model.LastName;
        staff.Gender = model.Gender;
        staff.BirthDate = model.BirthDate;
        staff.Address = model.Address;
        staff.ContactNumber = model.ContactNumber;
        staff.Email = model.Email;
        staff.Position = model.Position;
        staff.Department = model.Department;
        staff.EmploymentStatus = model.EmploymentStatus;
        staff.DateHired = model.DateHired ?? staff.DateHired;
        staff.IsActive = model.IsActive;
        staff.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Staff", "Staff", staff.Id, staff.FullName);
        TempData["Success"] = $"Staff member {staff.FullName} was updated.";
        return RedirectToAction(nameof(Details), new { id = staff.Id });
    }

    // ---------------------------------------------------------------- Delete
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.Id == id);
        if (staff is null) return NotFound();

        var name = staff.FullName;
        _db.StaffMembers.Remove(staff);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Staff", "Staff", id, name);
        TempData["Success"] = $"Staff member {name} was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // --------------------------------------------------------------- Helpers
    private async Task<List<SelectListItem>> BuildDepartmentOptionsAsync(string? selected)
    {
        var departments = await _db.StaffMembers
            .Where(s => s.Department != null && s.Department != "")
            .Select(s => s.Department!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

        return departments.Select(d => new SelectListItem(d, d, selected == d)).ToList();
    }

    private async Task<string> GenerateStaffNumberAsync()
    {
        const string prefix = "S-";
        var existing = await _db.StaffMembers
            .Where(s => s.StaffNumber.StartsWith(prefix))
            .Select(s => s.StaffNumber)
            .ToListAsync();

        var max = 0;
        foreach (var number in existing)
        {
            if (int.TryParse(number[prefix.Length..], out var value) && value > max) max = value;
        }

        return $"{prefix}{max + 1:D3}";
    }
}
