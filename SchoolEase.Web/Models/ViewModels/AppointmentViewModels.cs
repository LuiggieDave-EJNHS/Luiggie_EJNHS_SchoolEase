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
    [Display(Name = "Date")]
    public DateTime Date { get; set; } = DateTime.Today;

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
