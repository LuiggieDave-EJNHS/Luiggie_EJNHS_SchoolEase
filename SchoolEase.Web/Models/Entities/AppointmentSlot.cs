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
    public DateTime StartDateTime => Date.Date.Add(StartTime);

    [NotMapped]
    public bool IsPast => StartDateTime < DateTime.Now;

    /// <summary>Label such as "Nov 24, 2026 • 8:00 AM – 9:00 AM".</summary>
    [NotMapped]
    public string TimeLabel =>
        $"{Date:MMM d, yyyy} • {DateTime.Today.Add(StartTime):h:mm tt} – {DateTime.Today.Add(EndTime):h:mm tt}";
}
