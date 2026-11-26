using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

/// <summary>Staff-side list of published appointment slots.</summary>
public class AppointmentSlotIndexViewModel
{
    public List<AppointmentSlot> Slots { get; set; } = new();
    public int UpcomingCount { get; set; }
    public int TodayCount { get; set; }
    public int TotalBookings { get; set; }
    public int PendingRequests { get; set; }
}

/// <summary>Backing model for the staff "open a slot" form.</summary>
public class AppointmentSlotFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Purpose")]
    public AppointmentPurpose Purpose { get; set; } = AppointmentPurpose.Registrar;

    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime EndDate { get; set; } = DateTime.Today;

    [DataType(DataType.Time)]
    [Display(Name = "Start time")]
    public TimeSpan StartTime { get; set; } = new(8, 0, 0);

    [DataType(DataType.Time)]
    [Display(Name = "End time")]
    public TimeSpan EndTime { get; set; } = new(9, 0, 0);

    [Range(1, 200)]
    [Display(Name = "Seats")]
    public int Capacity { get; set; } = 5;

    [StringLength(160)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes for learners")]
    public string? Notes { get; set; }

    [Display(Name = "Open for booking")]
    public bool IsPublished { get; set; } = true;

    public List<SelectListItem> Purposes { get; set; } = new();
}

/// <summary>Backing model for a learner's slot-less appointment request.</summary>
public class AppointmentRequestFormViewModel
{
    [Display(Name = "Purpose")]
    public AppointmentPurpose Purpose { get; set; } = AppointmentPurpose.Registrar;

    [DataType(DataType.Date)]
    [Display(Name = "Preferred date")]
    public DateTime? PreferredDate { get; set; } = DateTime.Today.AddDays(1);

    [DataType(DataType.Time)]
    [Display(Name = "Preferred time")]
    public TimeSpan? PreferredTime { get; set; }

    [Required, StringLength(500)]
    [Display(Name = "What do you need?")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The document request number (e.g. DR-2026-0001) the learner is claiming.
    /// Required when the purpose is Document Claiming so staff can open the request in one click.
    /// </summary>
    [StringLength(30)]
    [Display(Name = "Document Request ID")]
    public string? DocumentRequestNumber { get; set; }

    public List<SelectListItem> Purposes { get; set; } = new();
}

/// <summary>Staff screen listing the learner requests that still need a slot.</summary>
public class AppointmentRequestsViewModel
{
    public List<Appointment> Requests { get; set; } = new();

    /// <summary>Slots staff can hand a request into (published, not past, with room left).</summary>
    public List<AppointmentSlot> OpenSlots { get; set; } = new();
}

/// <summary>A slot together with the bookings made against it.</summary>
public class AppointmentSlotDetailsViewModel
{
    public AppointmentSlot Slot { get; set; } = null!;
    public List<Appointment> Bookings { get; set; } = new();
}

/// <summary>Learner screen: my own bookings plus the slots still open to me.</summary>
public class MyAppointmentsViewModel
{
    public Student? Student { get; set; }
    public List<Appointment> Bookings { get; set; } = new();
    public List<AppointmentSlot> OpenSlots { get; set; } = new();
}
