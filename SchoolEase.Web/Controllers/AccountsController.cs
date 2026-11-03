using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class AccountsController : Controller
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public AccountsController(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUser = currentUser;
        _audit = audit;
    }

    // ------------------------------------------------------------------
    // List
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index(string? keyword, string? role, bool? active, int page = 1)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(u => u.FullName.Contains(k) || (u.Email ?? string.Empty).Contains(k));
        }

        if (active.HasValue) query = query.Where(u => u.IsActive == active.Value);

        if (!string.IsNullOrWhiteSpace(role) && Roles.All.Contains(role))
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            var ids = usersInRole.Select(u => u.Id).ToList();
            query = query.Where(u => ids.Contains(u.Id));
        }

        var paged = await PagedList<AppUser>.CreateAsync(query.OrderBy(u => u.FullName), page, PageSize);

        var rows = new List<AccountRow>();
        foreach (var user in paged)
        {
            rows.Add(new AccountRow { User = user, Roles = await _userManager.GetRolesAsync(user) });
        }

        var vm = new AccountListViewModel
        {
            Accounts = rows,
            Keyword = keyword,
            Role = role,
            Active = active
        };

        ViewData["Title"] = "User Accounts";
        ViewData["Pagination"] = new PaginationInfo
        {
            PageIndex = paged.PageIndex,
            TotalPages = paged.TotalPages,
            TotalCount = paged.TotalCount,
            PageSize = paged.PageSize,
            Action = "Index",
            Controller = "Accounts",
            RouteValues = new Dictionary<string, string?>
            {
                ["keyword"] = keyword,
                ["role"] = role,
                ["active"] = active?.ToString()
            }
        };

        return View(vm);
    }

    // ------------------------------------------------------------------
    // Details
    // ------------------------------------------------------------------
    public async Task<IActionResult> Details(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var vm = new AccountDetailsViewModel
        {
            User = user,
            Roles = await _userManager.GetRolesAsync(user),
            Student = await _db.Students.Include(s => s.Section).FirstOrDefaultAsync(s => s.UserId == id),
            Teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.UserId == id),
            Staff = await _db.StaffMembers.FirstOrDefaultAsync(s => s.UserId == id),
            RecentLogs = await _db.AuditLogs
                .Where(a => a.UserId == id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync()
        };

        ViewData["Title"] = user.FullName;
        return View(vm);
    }

    // ------------------------------------------------------------------
    // Create
    // ------------------------------------------------------------------
    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "New Account";
        return View(new AccountCreateViewModel { Role = Roles.Teacher, IsActive = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create(AccountCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "New Account";
            return View(model);
        }

        if (!CanAssignRole(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Only a Super Administrator may assign the Super Administrator role.");
            ViewData["Title"] = "New Account";
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            AddErrors(result);
            ViewData["Title"] = "New Account";
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded) AddErrors(roleResult);

        await _audit.LogAsync("Create Account", "Account", user.Id, $"Created {user.Email} as {model.Role}.");
        TempData["Success"] = $"Account for {user.FullName} created.";
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    // ------------------------------------------------------------------
    // Edit
    // ------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        ViewData["Title"] = $"Edit {user.FullName}";
        return View(new AccountEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? Roles.Student,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(AccountEditViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Edit {user.FullName}";
            return View(model);
        }

        if (!CanAssignRole(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Only a Super Administrator may assign the Super Administrator role.");
            ViewData["Title"] = $"Edit {user.FullName}";
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.IsActive = model.IsActive;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            AddErrors(update);
            ViewData["Title"] = $"Edit {user.FullName}";
            return View(model);
        }

        // Reassign roles: remove existing, then add the selected one.
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removed = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removed.Succeeded) AddErrors(removed);
        }

        if (!string.IsNullOrWhiteSpace(model.Role))
        {
            if (!await _roleManager.RoleExistsAsync(model.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.Role));
            }

            var added = await _userManager.AddToRoleAsync(user, model.Role);
            if (!added.Succeeded) AddErrors(added);
        }

        await _audit.LogAsync("Update Account", "Account", user.Id, $"Updated {user.Email} (role: {model.Role}).");
        TempData["Success"] = "Account updated.";
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    // ------------------------------------------------------------------
    // Reset password
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            TempData["Error"] = "The new password must be at least 8 characters long.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _userManager.RemovePasswordAsync(user);
        var result = await _userManager.AddPasswordAsync(user, newPassword);

        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Details), new { id });
        }

        await _audit.LogAsync("Reset Password", "Account", user.Id, $"Password reset for {user.Email}.");
        TempData["Success"] = $"Password for {user.FullName} was reset.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------
    // Toggle active
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == _currentUser.UserId)
        {
            TempData["Warning"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Details), new { id });
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync(user.IsActive ? "Activate Account" : "Deactivate Account", "Account", user.Id,
            $"{user.Email} is now {(user.IsActive ? "active" : "inactive")}.");

        TempData["Success"] = user.IsActive ? "Account activated." : "Account deactivated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------
    // Manually confirm email
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> ConfirmEmail(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.EmailConfirmed)
        {
            TempData["Info"] = "This account is already confirmed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        await _audit.LogAsync("Manual Email Confirmation", "Account", user.Id, $"Super Admin manually confirmed email for {user.Email}.");
        TempData["Success"] = $"Email for {user.FullName} has been manually confirmed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ------------------------------------------------------------------
    // Delete
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (user.Id == _currentUser.UserId)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var email = user.Email;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            AddErrors(result);
            TempData["Error"] = "The account could not be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _audit.LogAsync("Delete Account", "Account", id, $"Deleted account {email}.");
        TempData["Success"] = "Account deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private bool CanAssignRole(string role) =>
        role != Roles.SuperAdministrator || _currentUser.IsSuperAdministrator;

    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
