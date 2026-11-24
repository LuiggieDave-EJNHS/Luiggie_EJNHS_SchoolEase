using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A window of time the school opens for learner appointments (for example the registrar's
/// desk on a Tuesday morning). Staff publish slots; learners book a seat inside them, so a
/// slot holds up to <see cref="Capacity"/> active bookings at a time.
/// </summary>
public class AppointmentSlot
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

    [StringLength(120)]
    public string CreatedByName { get; set; } = string.Empty;

    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    /// <summary>Seats already taken by bookings that have not been cancelled.</summary>
    [NotMapped]
    public int BookedCount => Appointments.Count(a => a.Status == AppointmentStatus.Booked);

    [NotMapped]
    public int RemainingSeats => Math.Max(0, Capacity - BookedCount);

    [NotMapped]
    public bool IsFull => RemainingSeats <= 0;

    /// <summary>Start of the slot in local time, used to sort and to hide past slots.</summary>
    [NotMapped]
    public DateTime StartDateTime => StartDate.Date.Add(StartTime);

    /// <summary>End of the slot in local time. A multi-day slot only becomes "past" after this point.</summary>
    [NotMapped]
    public DateTime EndDateTime => EndDate.Date.Add(EndTime);

    [NotMapped]
    public bool IsPast => EndDateTime < DateTime.Now;

    /// <summary>True when the slot spans more than one calendar day.</summary>
    [NotMapped]
    public bool IsMultiDay => StartDate.Date != EndDate.Date;

    /// <summary>"Nov 24, 2026" for a single day, or "Nov 24 – 28, 2026" across a range.</summary>
    [NotMapped]
    public string DateLabel => IsMultiDay
        ? $"{StartDate:MMM d} – {EndDate:MMM d, yyyy}"
        : StartDate.ToString("MMM d, yyyy");

    /// <summary>Label such as "Nov 24 – 28, 2026 • 8:00 AM – 9:00 AM".</summary>
    [NotMapped]
    public string TimeLabel =>
        $"{DateLabel} • {DateTime.Today.Add(StartTime):h:mm tt} – {DateTime.Today.Add(EndTime):h:mm tt}";
}
