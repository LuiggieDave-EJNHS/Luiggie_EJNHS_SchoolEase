using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Infrastructure;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly UserManager<AppUser> _userManager;

    public SettingsController(
        ApplicationDbContext db,
        IConfiguration configuration,
        ICurrentUserService currentUser,
        IAuditService audit,
        UserManager<AppUser> userManager)
    {
        _db = db;
        _configuration = configuration;
        _currentUser = currentUser;
        _audit = audit;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var account = await _userManager.GetUserAsync(User);

        var vm = new SettingsIndexViewModel
        {
            Settings = await _db.SystemSettings
                .OrderBy(s => s.Group).ThenBy(s => s.Key)
                .ToListAsync(),
            Summary = new SystemSummaryViewModel
            {
                ProviderDisplayName = DatabaseSetup.ProviderDisplayName(_configuration),
                AspNetCoreVersion = $"ASP.NET Core (.NET {Environment.Version})",
                ServerTime = DateTime.Now,
                Students = await _db.Students.CountAsync(),
                Teachers = await _db.Teachers.CountAsync(),
                Staff = await _db.StaffMembers.CountAsync(),
                Sections = await _db.Sections.CountAsync(),
                Subjects = await _db.Subjects.CountAsync(),
                Users = await _db.Users.CountAsync()
            }
        };

        if (account is not null)
        {
            vm.HasRecoveryCode = !string.IsNullOrEmpty(await RecoveryCodes.GetHashAsync(_userManager, account));
            vm.RecoveryCodeCreatedAt = await RecoveryCodes.GetCreatedAtAsync(_userManager, account);
        }

        // Only a Super Administrator sees (and can run) the demo-data cleanup.
        if (User.IsInRole(Roles.SuperAdministrator))
        {
            vm.DemoData = await DemoDataPurge.ScanAsync(_db);
        }

        ViewData["Title"] = "System Settings";
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Dictionary<string, string?> values)
    {
        var settings = await _db.SystemSettings.ToListAsync();
        var changed = new List<string>();
        var now = DateTime.UtcNow;

        foreach (var setting in settings)
        {
            if (!values.TryGetValue(setting.Key, out var newValue)) continue;
            if (setting.Value == newValue) continue;

            setting.Value = newValue;
            setting.UpdatedAt = now;
            setting.UpdatedByUserId = _currentUser.UserId;
            setting.UpdatedByName = _currentUser.FullName;
            changed.Add(setting.Key);
        }

        if (changed.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        await _audit.LogAsync(
            "Update System Settings",
            "SystemSetting",
            null,
            changed.Count == 0 ? "No settings changed." : $"Updated: {string.Join(", ", changed)}");

        TempData["Success"] = "System settings saved.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Issues a fresh single-use recovery code for the signed-in Super Administrator.
    /// The plain text is shown once (via TempData) and only its hash is stored.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateRecoveryCode()
    {
        var account = await _userManager.GetUserAsync(User);
        if (account is null) return Forbid();

        var code = RecoveryCodes.Generate();
        await RecoveryCodes.StoreAsync(_userManager, account, code);

        await _audit.LogAsync("Generate Recovery Code", "Account", account.Id,
            $"{account.FullName} generated a new Super Administrator recovery code.",
            userName: account.FullName, userId: account.Id);

        // Surfaced once, then discarded — we never store the plain text.
        TempData["NewRecoveryCode"] = code;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Revokes the stored recovery code so it can no longer unlock the account.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRecoveryCode()
    {
        var account = await _userManager.GetUserAsync(User);
        if (account is null) return Forbid();

        await RecoveryCodes.ClearAsync(_userManager, account);

        await _audit.LogAsync("Revoke Recovery Code", "Account", account.Id,
            $"{account.FullName} revoked the Super Administrator recovery code.",
            userName: account.FullName, userId: account.Id);

        TempData["Success"] = "Recovery code revoked.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes every row the demo seeders created (sample learners, personnel,
    /// sections, timetable, records and announcements) while leaving real data and
    /// all lookups untouched. The operator has to type the confirmation phrase and
    /// the signed-in account is never removed, so a Super Administrator can never
    /// lock themselves out with this.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.SuperAdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeDemoData(string? confirmation)
    {
        if (!string.Equals(confirmation?.Trim(), "REMOVE DEMO DATA", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Nothing was deleted. Type REMOVE DEMO DATA exactly to confirm.";
            return RedirectToAction(nameof(Index));
        }

        var account = await _userManager.GetUserAsync(User);
        if (account is null) return Forbid();

        var report = await DemoDataPurge.PurgeAsync(_db, account.Id);

        // Identity rows are removed through UserManager so the role, claim and
        // login tables stay consistent. The signed-in account is skipped.
        var removedAccounts = 0;
        foreach (var email in DemoDataPurge.DemoUserEmails)
        {
            var demoUser = await _userManager.FindByEmailAsync(email);
            if (demoUser is null || demoUser.Id == account.Id) continue;

            var result = await _userManager.DeleteAsync(demoUser);
            if (result.Succeeded) removedAccounts++;
        }

        report.Users = removedAccounts;

        await _audit.LogAsync("Purge Demo Data", "Database", null,
            report.IsEmpty
                ? "No demo rows were found."
                : $"Removed {report.Total} demo row(s) including {report.Students} learner(s), " +
                  $"{report.Teachers + report.Staff} personnel record(s), {report.Sections} section(s) " +
                  $"and {report.AttendanceRecords + report.GradeRecords} academic record(s).",
            userName: account.FullName, userId: account.Id);

        TempData["Success"] = report.IsEmpty
            ? "No demo data was found — the database is already clean."
            : $"Demo data removed: {report.Users} account(s), {report.Students} learner(s), " +
              $"{report.Teachers + report.Staff} personnel, {report.Sections} section(s) and " +
              $"{report.AttendanceRecords + report.GradeRecords} academic record(s).";
        return RedirectToAction(nameof(Index));
    }
}
