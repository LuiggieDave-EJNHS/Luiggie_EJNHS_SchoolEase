using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A learner's booking inside an <see cref="AppointmentSlot"/>.</summary>
public class Appointment
{
    public int Id { get; set; }

    [Display(Name = "Slot")]
    public int SlotId { get; set; }
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

    [Required, StringLength(500)]
    [Display(Name = "Reason")]
    public string Reason { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;

    [StringLength(500)]
    [Display(Name = "Staff remarks")]
    public string? StaffRemarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
