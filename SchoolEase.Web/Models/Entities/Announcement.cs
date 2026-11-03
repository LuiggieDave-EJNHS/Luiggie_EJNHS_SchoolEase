using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A bulletin posted to the dashboard of selected audiences.</summary>
public class Announcement
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    [Display(Name = "Content")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Author")]
    public string? AuthorUserId { get; set; }
    public AppUser? Author { get; set; }

    [StringLength(120)]
    [Display(Name = "Posted By")]
    public string AuthorName { get; set; } = string.Empty;

    [Display(Name = "Priority")]
    public AnnouncementPriority Priority { get; set; } = AnnouncementPriority.Normal;

    [Display(Name = "Target Audience")]
    public Audience Audience { get; set; } = Audience.All;

    [DataType(DataType.DateTime)]
    [Display(Name = "Publish Date")]
    public DateTime PublishDate { get; set; } = DateTime.Now;

    [DataType(DataType.DateTime)]
    [Display(Name = "Expiry Date")]
    public DateTime? ExpireDate { get; set; }

    [Display(Name = "Published")]
    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsLive => IsPublished && PublishDate <= DateTime.Now && (ExpireDate is null || ExpireDate >= DateTime.Now);

    /// <summary>Bootstrap contextual colour used for the priority chip.</summary>
    public string PriorityCss => Priority switch
    {
        AnnouncementPriority.Urgent => "danger",
        AnnouncementPriority.High => "warning",
        AnnouncementPriority.Low => "secondary",
        _ => "info"
    };
}
