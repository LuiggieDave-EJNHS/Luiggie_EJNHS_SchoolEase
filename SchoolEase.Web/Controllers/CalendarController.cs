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
public class CalendarController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CalendarController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    // ------------------------------------------------------------------
    // Month grid (everyone)
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (m < 1) { m = 12; y--; }
        if (m > 12) { m = 1; y++; }

        var firstOfMonth = new DateTime(y, m, 1);
        var daysInMonth = DateTime.DaysInMonth(y, m);
        var startOffset = (int)firstOfMonth.DayOfWeek; // Sunday = 0
        var totalCells = (int)Math.Ceiling((startOffset + daysInMonth) / 7.0) * 7;

        var monthEnd = firstOfMonth.AddMonths(1);
        var audiences = VisibleAudiences();

        var monthEvents = await _db.SchoolEvents
            .Where(e => audiences.Contains(e.Audience) && e.StartDate >= firstOfMonth && e.StartDate < monthEnd)
            .OrderBy(e => e.StartDate)
            .ToListAsync();

        var days = new List<CalendarDay>();
        for (var i = 0; i < totalCells; i++)
        {
            var dayNumber = i - startOffset + 1;
            if (dayNumber < 1 || dayNumber > daysInMonth)
            {
                days.Add(new CalendarDay { IsCurrentMonth = false });
                continue;
            }

            var date = new DateTime(y, m, dayNumber);
            days.Add(new CalendarDay
            {
                Date = date,
                DayNumber = dayNumber,
                IsCurrentMonth = true,
                IsToday = date == today,
                Events = monthEvents
                    .Where(e => e.StartDate.Date <= date && (e.EndDate ?? e.StartDate).Date >= date)
                    .ToList()
            });
        }

        var previous = firstOfMonth.AddMonths(-1);
        var next = firstOfMonth.AddMonths(1);

        var vm = new CalendarIndexViewModel
        {
            Year = y,
            Month = m,
            MonthLabel = firstOfMonth.ToString("MMMM yyyy"),
            PreviousYear = previous.Year,
            PreviousMonth = previous.Month,
            NextYear = next.Year,
            NextMonth = next.Month,
            Days = days,
            MonthEvents = monthEvents,
            Upcoming = await _db.SchoolEvents
                .Where(e => audiences.Contains(e.Audience) && e.StartDate >= DateTime.Now)
                .OrderBy(e => e.StartDate)
                .Take(8)
                .ToListAsync(),
            CanManage = _currentUser.IsAdministrator
        };

        ViewData["Title"] = "Calendar";
        return View(vm);
    }

    // ------------------------------------------------------------------
    // Create (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.AdminOnly)]
    [HttpGet]
    public IActionResult Create(int? year, int? month)
    {
        var today = DateTime.Today;
        var start = year.HasValue && month.HasValue
            ? new DateTime(year.Value, month.Value, 1).AddHours(8)
            : today.AddHours(8);

        ViewData["Title"] = "New Event";
        return View(new SchoolEvent { StartDate = start, AllDay = true, Audience = Audience.All });
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> Create(SchoolEvent model)
    {
        if (!ModelState.IsValid) return View(model);

        model.CreatedByUserId = _currentUser.UserId;
        model.CreatedAt = DateTime.UtcNow;

        _db.SchoolEvents.Add(model);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Create Event", "SchoolEvent", model.Id, $"Scheduled \"{model.Title}\".");
        TempData["Success"] = "Event added to the calendar.";
        return RedirectToAction(nameof(Index), new { year = model.StartDate.Year, month = model.StartDate.Month });
    }

    // ------------------------------------------------------------------
    // Edit (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var schoolEvent = await _db.SchoolEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (schoolEvent is null) return NotFound();

        ViewData["Title"] = $"Edit {schoolEvent.Title}";
        return View(schoolEvent);
    }

    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, SchoolEvent model)
    {
        var schoolEvent = await _db.SchoolEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (schoolEvent is null) return NotFound();

        if (!ModelState.IsValid) return View(model);

        schoolEvent.Title = model.Title;
        schoolEvent.Description = model.Description;
        schoolEvent.StartDate = model.StartDate;
        schoolEvent.EndDate = model.EndDate;
        schoolEvent.AllDay = model.AllDay;
        schoolEvent.Category = model.Category;
        schoolEvent.Location = model.Location;
        schoolEvent.Audience = model.Audience;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Event", "SchoolEvent", schoolEvent.Id, $"Updated \"{schoolEvent.Title}\".");
        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Index), new { year = schoolEvent.StartDate.Year, month = schoolEvent.StartDate.Month });
    }

    // ------------------------------------------------------------------
    // Delete (administrators)
    // ------------------------------------------------------------------
    [Authorize(Policy = Policies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var schoolEvent = await _db.SchoolEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (schoolEvent is null) return NotFound();

        _db.SchoolEvents.Remove(schoolEvent);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Event", "SchoolEvent", id, $"Removed \"{schoolEvent.Title}\".");
        TempData["Success"] = "Event deleted.";
        return RedirectToAction(nameof(Index), new { year = schoolEvent.StartDate.Year, month = schoolEvent.StartDate.Month });
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private List<Audience> VisibleAudiences()
    {
        var list = new List<Audience> { Audience.All };

        if (_currentUser.IsAdministrator)
        {
            list.AddRange(new[] { Audience.Students, Audience.Teachers, Audience.Staff, Audience.Administrators });
        }
        else if (_currentUser.IsTeacher)
        {
            list.Add(Audience.Teachers);
        }
        else if (_currentUser.IsRegistrar || _currentUser.IsIctHeadCoordinator)
        {
            list.Add(Audience.Staff);
        }
        else if (_currentUser.IsStudent)
        {
            list.Add(Audience.Students);
        }

        return list;
    }
}
