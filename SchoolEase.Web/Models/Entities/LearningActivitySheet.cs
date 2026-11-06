using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A Learning Activity Sheet (LAS) published to the school library.
/// The file itself is stored in the database so it survives hosting redeploys
/// (free hosting tiers use an ephemeral disk).
/// </summary>
public class LearningActivitySheet
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Display(Name = "Grade Level")]
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }

    [Range(1, 3)]
    [Display(Name = "Term")]
    public int Term { get; set; } = 1;

    [Display(Name = "Week")]
    public int? WeekNumber { get; set; }

    [StringLength(40)]
    [Display(Name = "MELC Code")]
    public string? MelcCode { get; set; }

    [StringLength(400)]
    [Display(Name = "Competency")]
    public string? Competency { get; set; }

    [Required, StringLength(260)]
    [Display(Name = "File Name")]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Display(Name = "File Size")]
    public long FileSize { get; set; }

    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    [Display(Name = "Published")]
    public bool IsPublished { get; set; } = true;

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    [StringLength(120)]
    [Display(Name = "Uploaded By")]
    public string UploaderName { get; set; } = string.Empty;

    public string? UploadedByUserId { get; set; }

    [Display(Name = "Downloads")]
    public int DownloadCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Human-readable file size for the UI.</summary>
    [NotMapped]
    public string FileSizeLabel => FileSizeLabelOf(FileSize);

    /// <summary>"Week 3" label, or a fallback when no week is set.</summary>
    [NotMapped]
    public string WeekLabel => WeekNumber is > 0 ? $"Week {WeekNumber}" : "Any week";

    /// <summary>Formats a byte count as B / KB / MB.</summary>
    public static string FileSizeLabelOf(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.#} MB",
        >= 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes} B"
    };
}
