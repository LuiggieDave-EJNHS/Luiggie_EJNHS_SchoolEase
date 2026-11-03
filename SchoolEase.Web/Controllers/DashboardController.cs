using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _db;

    public DashboardController(IDashboardService dashboard, ICurrentUserService currentUser, ApplicationDbContext db)
    {
        _dashboard = dashboard;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        if (_currentUser.IsStudent) return RedirectToAction("Dashboard", "Student");
        if (_currentUser.IsTeacher) return RedirectToAction("Dashboard", "Teacher");

        var vm = await _dashboard.BuildAdminDashboardAsync();
        vm.Notifications = await LoadNotificationsAsync();

        ViewData["Title"] = _currentUser.HasSchoolWideAccess ? "Dashboard" : "Staff Dashboard";
        return View(vm);
    }

    private async Task<List<Notification>> LoadNotificationsAsync()
    {
        var uid = _currentUser.UserId;
        if (uid is null) return new List<Notification>();

        return await _db.Notifications
            .Where(n => n.UserId == uid)
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .ToListAsync();
    }
}
