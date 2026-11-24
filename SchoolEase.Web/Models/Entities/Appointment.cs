using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A learner's appointment. It either sits inside an <see cref="AppointmentSlot"/>
/// (a confirmed booking) or stands on its own as a <see cref="AppointmentStatus.Requested"/>
/// request when no slot is open yet.
/// </summary>
public class Appointment
{
    public int Id { get; set; }

    /// <summary>Null while the appointment is only a request and staff have not assigned a slot yet.</summary>
    [Display(Name = "Slot")]
    public int? SlotId { get; set; }
    public AppointmentSlot? Slot { get; set; }

    /// <summary>The learner record, when the booker has one. Optional so keep-sakes work.</summary>
    [Display(Name = "Learner")]
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    /// <summary>The account that booked, so a learner only ever sees their own bookings.</summary>
    public string? UserId { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Learner Name")]
    public string StudentName { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Student Number")]
    public string? StudentNumber { get; set; }

    /// <summary>Why the learner wants to see school staff. Kept on the request itself for slot-less requests.</summary>
    [Display(Name = "Purpose")]
    public AppointmentPurpose Purpose { get; set; } = AppointmentPurpose.Registrar;

    [Required, StringLength(500)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Day the learner picked (inside the slot's range, or the day hoped for on a request).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Preferred date")]
    public DateTime? PreferredDate { get; set; }

    /// <summary>Time of day the learner hoped for on a request, before staff assign a slot.</summary>
    [DataType(DataType.Time)]
    [Display(Name = "Preferred time")]
    public TimeSpan? PreferredTime { get; set; }

    [Display(Name = "Status")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    [StringLength(500)]
    [Display(Name = "Staff remarks")]
    public string? StaffRemarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>True when the learner asked for a time but no slot has been assigned yet.</summary>
    [NotMapped]
    public bool IsPendingRequest => Status == AppointmentStatus.Requested && Slot is null;

    /// <summary>Purpose shown to users: the slot's purpose when booked, otherwise the request's own purpose.</summary>
    [NotMapped]
    public AppointmentPurpose EffectivePurpose => Slot?.Purpose ?? Purpose;

    /// <summary>Human-readable schedule: the confirmed slot time, or the requested day/time.</summary>
    [NotMapped]
    public string ScheduleLabel
    {
        get
        {
            if (Slot is not null)
            {
                var day = (PreferredDate ?? Slot.StartDate).ToString("MMM d, yyyy");
                var from = DateTime.Today.Add(Slot.StartTime).ToString("h:mm tt");
                var to = DateTime.Today.Add(Slot.EndTime).ToString("h:mm tt");
                return $"{day} • {from} – {to}";
            }

            var requestedDay = PreferredDate?.ToString("MMM d, yyyy") ?? "Any day";
            var requestedTime = PreferredTime.HasValue
                ? DateTime.Today.Add(PreferredTime.Value).ToString("h:mm tt")
                : "Any time";
            return $"{requestedDay} • {requestedTime}";
        }
    }
}
