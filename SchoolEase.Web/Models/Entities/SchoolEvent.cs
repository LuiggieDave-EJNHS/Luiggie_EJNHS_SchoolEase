using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>An entry on the school calendar (event, holiday, exam, meeting, deadline).</summary>
public class SchoolEvent
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Start")]
    public DateTime StartDate { get; set; } = DateTime.Now;

    [DataType(DataType.DateTime)]
    [Display(Name = "End")]
    public DateTime? EndDate { get; set; }

    [Display(Name = "Whole Day")]
    public bool AllDay { get; set; } = true;

    [Display(Name = "Category")]
    public EventCategory Category { get; set; } = EventCategory.Event;

    [StringLength(120)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [Display(Name = "Target Audience")]
    public Audience Audience { get; set; } = Audience.All;

    [Display(Name = "Created By")]
    public string? CreatedByUserId { get; set; }
    public AppUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Hex colour used by the calendar view.</summary>
    public string ColorHex => Category switch
    {
        EventCategory.Holiday => "#dc3545",
        EventCategory.Exam => "#fd7e14",
        EventCategory.Meeting => "#6f42c1",
        EventCategory.Activity => "#20c997",
        EventCategory.Deadline => "#d63384",
        _ => "#0d6efd"
    };
}
