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

/// <summary>
/// Staff publish appointment slots (for example the registrar's desk for a week) and
/// learners book a seat inside them. Learners (current and former students, plus teachers)
/// may also raise a request for a time even when no slot is open yet; staff then assign a slot.
/// </summary>
[Authorize]
public class AppointmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public AppointmentsController(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuditService audit,
        INotificationService notify)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
        _notify = notify;
    }

    /// <summary>True for accounts that manage slots: administrators, the ICT coordinator, teachers and the registrar.</summary>
    private bool IsStaff =>
        _currentUser.IsAdministrator || _currentUser.IsIctHeadCoordinator
        || _currentUser.IsTeacher || _currentUser.IsRegistrar;

    /// <summary>
    /// True for accounts that may book or request a seat. Every signed-in account qualifies:
    /// learners (current and former), teachers and the back-office staff alike.
    /// </summary>
    private bool CanBook => _currentUser.IsAuthenticated;

    // ------------------------------------------------------------------
    // Staff: manage slots
    // ------------------------------------------------------------------
    public async Task<IActionResult> Index()
    {
        if (_currentUser.IsStudent) return RedirectToAction(nameof(MyAppointments));
        if (!IsStaff) return Forbid();

        var slots = (await _db.AppointmentSlots
                .Include(s => s.Appointments)
                .ToListAsync())
            .OrderBy(s => s.IsPast)
            .ThenBy(s => s.StartDate).ThenBy(s => s.StartTime)
            .ToList();

        var today = DateTime.Today;

        var pendingRequests = await _db.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.Requested && a.SlotId == null);

        var vm = new AppointmentSlotIndexViewModel
        {
            Slots = slots,
            UpcomingCount = slots.Count(s => !s.IsPast),
            TodayCount = slots.Count(s => s.StartDate.Date <= today && s.EndDate.Date >= today),
            TotalBookings = slots.Sum(s => s.BookedCount),
            PendingRequests = pendingRequests
        };

        ViewData["Title"] = "Appointments";
        return View(vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsStaff) return Forbid();

        ViewData["Title"] = "Open Appointment Slot";
        return View(BuildForm(new AppointmentSlotFormViewModel()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(AppointmentSlotFormViewModel model)
    {
        if (!IsStaff) return Forbid();

        Validate(model);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Open Appointment Slot";
            return View(BuildForm(model));
        }

        var slot = new AppointmentSlot
        {
            Title = model.Title.Trim(),
            Purpose = model.Purpose,
            StartDate = model.StartDate.Date,
            EndDate = model.EndDate.Date,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            Capacity = model.Capacity,
            Location = model.Location?.Trim(),
            Notes = model.Notes?.Trim(),
            IsPublished = model.IsPublished,
            CreatedByName = _currentUser.FullName,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.AppointmentSlots.Add(slot);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Open Appointment Slot", "AppointmentSlot", slot.Id,
            $"Opened slot \"{slot.Title}\" for {slot.DateLabel}.");
        TempData["Success"] = "Appointment slot opened.";
        return RedirectToAction(nameof(Details), new { id = slot.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!IsStaff) return Forbid();

        var slot = await _db.AppointmentSlots.FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null) return NotFound();

        ViewData["Title"] = $"Edit {slot.Title}";
        return View(BuildForm(new AppointmentSlotFormViewModel
        {
            Id = slot.Id,
            Title = slot.Title,
            Purpose = slot.Purpose,
            StartDate = slot.StartDate,
            EndDate = slot.EndDate,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Capacity = slot.Capacity,
            Location = slot.Location,
            Notes = slot.Notes,
            IsPublished = slot.IsPublished
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, AppointmentSlotFormViewModel model)
    {
        if (!IsStaff) return Forbid();

        var slot = await _db.AppointmentSlots.Include(s => s.Appointments).FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null) return NotFound();

        Validate(model);
        if (model.Capacity < slot.BookedCount)
        {
            ModelState.AddModelError(nameof(model.Capacity),
                $"Seats cannot be fewer than the {slot.BookedCount} booking(s) already made.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"Edit {slot.Title}";
            model.Id = id;
            return View(BuildForm(model));
        }

        slot.Title = model.Title.Trim();
        slot.Purpose = model.Purpose;
        slot.StartDate = model.StartDate.Date;
        slot.EndDate = model.EndDate.Date;
        slot.StartTime = model.StartTime;
        slot.EndTime = model.EndTime;
        slot.Capacity = model.Capacity;
        slot.Location = model.Location?.Trim();
        slot.Notes = model.Notes?.Trim();
        slot.IsPublished = model.IsPublished;
        slot.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync("Update Appointment Slot", "AppointmentSlot", slot.Id, $"Updated slot \"{slot.Title}\".");
        TempData["Success"] = "Appointment slot updated.";
        return RedirectToAction(nameof(Details), new { id = slot.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!IsStaff) return Forbid();

        var slot = await _db.AppointmentSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null) return NotFound();

        var vm = new AppointmentSlotDetailsViewModel
        {
            Slot = slot,
            Bookings = slot.Appointments
                .OrderBy(a => a.Status == AppointmentStatus.Booked ? 0 : 1)
                .ThenBy(a => a.CreatedAt)
                .ToList()
        };

        ViewData["Title"] = slot.Title;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsStaff) return Forbid();

        var slot = await _db.AppointmentSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (slot is null) return NotFound();

        if (slot.Appointments.Any(a => a.Status == AppointmentStatus.Booked))
        {
            TempData["Error"] = "This slot still has active bookings. Complete or cancel them first, or unpublish the slot.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.AppointmentSlots.Remove(slot);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Delete Appointment Slot", "AppointmentSlot", id, $"Removed slot \"{slot.Title}\".");
        TempData["Success"] = "Appointment slot deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ------------------------------------------------------------------
    // Staff: process a booking
    // ------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> UpdateBooking(int id, AppointmentStatus status, string? remarks)
    {
        if (!IsStaff) return Forbid();

        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (appointment is null) return NotFound();

        appointment.Status = status;
        appointment.StaffRemarks = remarks?.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(appointment.UserId))
        {
            await _notify.NotifyAsync(
                appointment.UserId,
                $"Appointment {status.DisplayName()}",
                $"Your appointment \"{appointment.Slot?.Title ?? appointment.EffectivePurpose.DisplayName()}\" is now {status.DisplayName()}.",
                status == AppointmentStatus.Completed ? NotificationType.Success : NotificationType.Info,
                Url.Action(nameof(MyAppointments)));
        }

        await _audit.LogAsync("Update Appointment", "Appointment", appointment.Id,
            $"Appointment for {appointment.StudentName} set to {status.DisplayName()}.");
        TempData["Success"] = "Appointment updated.";
        return appointment.SlotId is int slotId
            ? RedirectToAction(nameof(Details), new { id = slotId })
            : RedirectToAction(nameof(Requests));
    }

    // ------------------------------------------------------------------
    // Staff: work the request queue
    // ------------------------------------------------------------------
    public async Task<IActionResult> Requests()
    {
        if (!IsStaff) return Forbid();

        var requests = await _db.Appointments
            .Where(a => a.Status == AppointmentStatus.Requested && a.SlotId == null)
            .OrderBy(a => a.PreferredDate).ThenBy(a => a.CreatedAt)
            .ToListAsync();

        var openSlots = (await _db.AppointmentSlots
                .Include(s => s.Appointments)
                .Where(s => s.IsPublished)
                .ToListAsync())
            .Where(s => !s.IsPast && !s.IsFull)
            .OrderBy(s => s.StartDate).ThenBy(s => s.StartTime)
            .ToList();

        ViewData["Title"] = "Appointment Requests";
        return View(new AppointmentRequestsViewModel { Requests = requests, OpenSlots = openSlots });
    }

    /// <summary>Staff approve a request by handing it into an open slot.</summary>
    [HttpPost]
    public async Task<IActionResult> Approve(int id, int slotId, string? remarks)
    {
        if (!IsStaff) return Forbid();

        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appointment is null) return NotFound();

        var slot = await _db.AppointmentSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot is null) return NotFound();

        if (!slot.IsPublished || slot.IsPast)
        {
            TempData["Error"] = "That slot is not open for booking any more.";
            return RedirectToAction(nameof(Requests));
        }

        if (slot.IsFull)
        {
            TempData["Error"] = "That slot has no seats left. Pick another one.";
            return RedirectToAction(nameof(Requests));
        }

        appointment.SlotId = slot.Id;
        appointment.Status = AppointmentStatus.Booked;
        appointment.StaffRemarks = remarks?.Trim();
        // Keep the learner's chosen day when it falls inside the slot's range.
        if (appointment.PreferredDate is null
            || appointment.PreferredDate.Value.Date < slot.StartDate.Date
            || appointment.PreferredDate.Value.Date > slot.EndDate.Date)
        {
            appointment.PreferredDate = slot.StartDate.Date;
        }
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(appointment.UserId))
        {
            await _notify.NotifyAsync(
                appointment.UserId,
                "Appointment confirmed",
                $"Your appointment has been set for {appointment.ScheduleLabel}.",
                NotificationType.Success,
                Url.Action(nameof(MyAppointments)));
        }

        await _audit.LogAsync("Approve Appointment Request", "Appointment", appointment.Id,
            $"{appointment.StudentName} assigned to slot \"{slot.Title}\".");
        TempData["Success"] = "Request approved and the learner booked in.";
        return RedirectToAction(nameof(Requests));
    }

    /// <summary>Staff decline a request they cannot accommodate.</summary>
    [HttpPost]
    public async Task<IActionResult> Decline(int id, string? remarks)
    {
        if (!IsStaff) return Forbid();

        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
        if (appointment is null) return NotFound();

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.StaffRemarks = remarks?.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(appointment.UserId))
        {
            await _notify.NotifyAsync(
                appointment.UserId,
                "Appointment request declined",
                string.IsNullOrWhiteSpace(remarks)
                    ? "We could not accommodate your requested appointment time."
                    : remarks!.Trim(),
                NotificationType.Warning,
                Url.Action(nameof(MyAppointments)));
        }

        await _audit.LogAsync("Decline Appointment Request", "Appointment", appointment.Id,
            $"Declined request from {appointment.StudentName}.");
        TempData["Success"] = "Request declined and the learner was notified.";
        return RedirectToAction(nameof(Requests));
    }

    // ------------------------------------------------------------------
    // Learner: book, request and track
    // ------------------------------------------------------------------
    public async Task<IActionResult> MyAppointments()
    {
        if (!CanBook) return RedirectToAction(nameof(Index));

        var student = await GetCurrentStudentAsync();
        var userId = _currentUser.UserId;

        var bookings = (await _db.Appointments
                .Include(a => a.Slot)
                .Where(a => a.UserId == userId)
                .ToListAsync())
            .OrderBy(a => a.SlotId is null ? 1 : 0)
            .ThenByDescending(a => a.CreatedAt)
            .ToList();

        var activeSlotIds = bookings
            .Where(a => a.Status == AppointmentStatus.Booked && a.SlotId is not null)
            .Select(a => a.SlotId!.Value)
            .ToHashSet();

        var openSlots = (await _db.AppointmentSlots
                .Include(s => s.Appointments)
                .Where(s => s.IsPublished)
                .ToListAsync())
            .Where(s => !s.IsPast && !s.IsFull && !activeSlotIds.Contains(s.Id))
            .OrderBy(s => s.StartDate).ThenBy(s => s.StartTime)
            .ToList();

        ViewData["Title"] = "My Appointments";
        return View(new MyAppointmentsViewModel
        {
            Student = student,
            Bookings = bookings,
            OpenSlots = openSlots
        });
    }

    [HttpGet]
    public async Task<IActionResult> Book(int slotId)
    {
        if (!CanBook) return RedirectToAction(nameof(Index));

        var slot = await _db.AppointmentSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot is null) return NotFound();

        if (!slot.IsPublished || slot.IsPast || slot.IsFull)
        {
            TempData["Error"] = "This appointment slot is no longer available for booking.";
            return RedirectToAction(nameof(MyAppointments));
        }

        if (await HasActiveBookingAsync(slotId))
        {
            TempData["Warning"] = "You already have a booking in this slot.";
            return RedirectToAction(nameof(MyAppointments));
        }

        ViewData["Title"] = "Book Appointment";
        return View(slot);
    }

    [HttpPost]
    public async Task<IActionResult> Book(int slotId, string reason, DateTime? preferredDate)
    {
        if (!CanBook) return RedirectToAction(nameof(Index));

        var slot = await _db.AppointmentSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.Id == slotId);
        if (slot is null) return NotFound();

        if (!slot.IsPublished || slot.IsPast || slot.IsFull)
        {
            TempData["Error"] = "This appointment slot is no longer available for booking.";
            return RedirectToAction(nameof(MyAppointments));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            ModelState.AddModelError(nameof(reason), "Please tell us the reason for your appointment.");
        }

        var chosenDate = (preferredDate ?? slot.StartDate).Date;
        if (chosenDate < slot.StartDate.Date || chosenDate > slot.EndDate.Date)
        {
            ModelState.AddModelError(nameof(preferredDate), "Pick a day inside the slot's date range.");
        }
        else if (chosenDate < DateTime.Today)
        {
            ModelState.AddModelError(nameof(preferredDate), "Pick today or a later day.");
        }

        if (await HasActiveBookingAsync(slotId))
        {
            ModelState.AddModelError(string.Empty, "You already have a booking in this slot.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Book Appointment";
            return View(slot);
        }

        var student = await GetCurrentStudentAsync();
        var appointment = new Appointment
        {
            SlotId = slot.Id,
            StudentId = student?.Id,
            UserId = _currentUser.UserId,
            StudentName = student?.FullName ?? _currentUser.FullName,
            StudentNumber = student?.StudentNumber,
            Purpose = slot.Purpose,
            Reason = reason.Trim(),
            PreferredDate = chosenDate,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        await _notify.NotifyAdministratorsAsync(
            "New appointment booking",
            $"{appointment.StudentName} booked \"{slot.Title}\" for {appointment.ScheduleLabel}.",
            NotificationType.Info,
            Url.Action(nameof(Details), new { id = slot.Id }));

        await _audit.LogAsync("Book Appointment", "Appointment", appointment.Id,
            $"{appointment.StudentName} booked slot {slot.Id}.");

        TempData["Success"] = "Your appointment has been booked.";
        return RedirectToAction(nameof(MyAppointments));
    }

    /// <summary>A learner asks for a time even when no slot is open. Staff fill it later.</summary>
    [HttpGet]
    public IActionResult RequestAppointment()
    {
        if (!CanBook) return RedirectToAction(nameof(Index));

        ViewData["Title"] = "Request an Appointment";
        return View(BuildRequestForm(new AppointmentRequestFormViewModel()));
    }

    [HttpPost]
    public async Task<IActionResult> RequestAppointment(AppointmentRequestFormViewModel model)
    {
        if (!CanBook) return RedirectToAction(nameof(Index));

        if (model.PreferredDate is { } wanted && wanted.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.PreferredDate), "Pick today or a later day.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Request an Appointment";
            return View(BuildRequestForm(model));
        }

        var student = await GetCurrentStudentAsync();
        var appointment = new Appointment
        {
            SlotId = null,
            StudentId = student?.Id,
            UserId = _currentUser.UserId,
            StudentName = student?.FullName ?? _currentUser.FullName,
            StudentNumber = student?.StudentNumber,
            Purpose = model.Purpose,
            Reason = model.Reason.Trim(),
            PreferredDate = model.PreferredDate?.Date,
            PreferredTime = model.PreferredTime,
            Status = AppointmentStatus.Requested,
            CreatedAt = DateTime.UtcNow
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        await _notify.NotifyAdministratorsAsync(
            "New appointment request",
            $"{appointment.StudentName} requested a {appointment.EffectivePurpose.DisplayName()} appointment on {appointment.ScheduleLabel}.",
            NotificationType.Info,
            Url.Action(nameof(Requests)));

        await _audit.LogAsync("Request Appointment", "Appointment", appointment.Id,
            $"{appointment.StudentName} requested an appointment.");

        TempData["Success"] = "Your request has been sent. You will be notified once a time is set.";
        return RedirectToAction(nameof(MyAppointments));
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!CanBook) return Forbid();

        var appointment = await _db.Appointments
            .Include(a => a.Slot)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (appointment is null) return NotFound();

        if (appointment.UserId != _currentUser.UserId) return Forbid();

        if (appointment.Status != AppointmentStatus.Booked && appointment.Status != AppointmentStatus.Requested)
        {
            TempData["Error"] = "Only active bookings or requests can be cancelled.";
            return RedirectToAction(nameof(MyAppointments));
        }

        var wasRequest = appointment.Status == AppointmentStatus.Requested;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(wasRequest ? "Cancel Appointment Request" : "Cancel Appointment",
            "Appointment", appointment.Id, $"{appointment.StudentName} cancelled {appointment.ScheduleLabel}.");
        TempData["Success"] = wasRequest ? "Your request has been withdrawn." : "Your appointment has been cancelled.";
        return RedirectToAction(nameof(MyAppointments));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private void Validate(AppointmentSlotFormViewModel model)
    {
        if (model.EndTime <= model.StartTime)
        {
            ModelState.AddModelError(nameof(model.EndTime), "The end time must be after the start time.");
        }

        if (model.StartDate.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.StartDate), "The start date cannot be in the past.");
        }

        if (model.EndDate.Date < model.StartDate.Date)
        {
            ModelState.AddModelError(nameof(model.EndDate), "The end date must be on or after the start date.");
        }
    }

    private static AppointmentSlotFormViewModel BuildForm(AppointmentSlotFormViewModel model)
    {
        model.Purposes = SelectLists.EnumOptions<AppointmentPurpose>(model.Purpose);
        return model;
    }

    private static AppointmentRequestFormViewModel BuildRequestForm(AppointmentRequestFormViewModel model)
    {
        model.Purposes = SelectLists.EnumOptions<AppointmentPurpose>(model.Purpose);
        return model;
    }

    private Task<Student?> GetCurrentStudentAsync() =>
        _db.Students.FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId);

    private Task<bool> HasActiveBookingAsync(int slotId) =>
        _db.Appointments.AnyAsync(a => a.SlotId == slotId
            && a.UserId == _currentUser.UserId
            && a.Status == AppointmentStatus.Booked);
}
