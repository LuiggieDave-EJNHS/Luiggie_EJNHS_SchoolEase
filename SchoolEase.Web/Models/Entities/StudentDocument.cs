using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A scanned record kept in a learner's 201 file. Staff upload the file; the learner
/// can read and download it from their own portal. Like the activity sheets, the bytes
/// live in object storage when a bucket is configured and in the database otherwise,
/// so the record survives hosting redeploys either way.
/// </summary>
public class StudentDocument
{
    public int Id { get; set; }

    [Display(Name = "Learner")]
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public StudentDocumentCategory Category { get; set; } = StudentDocumentCategory.Other;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required, StringLength(260)]
    [Display(Name = "File Name")]
    public string FileName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string ContentType { get; set; } = "application/octet-stream";

    [Display(Name = "File Size")]
    public long FileSize { get; set; }

    /// <summary>
    /// The file bytes. Kept empty when <see cref="StorageKey"/> points at an
    /// object-storage copy, so the column stays non-nullable on every provider.
    /// </summary>
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>Object key inside the configured bucket; null when the bytes are in the database.</summary>
    [StringLength(400)]
    public string? StorageKey { get; set; }

    /// <summary>When false the record is kept for staff only (e.g. confidential material).</summary>
    [Display(Name = "Share with learner")]
    public bool IsVisibleToStudent { get; set; } = true;

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
    public string FileSizeLabel => LearningActivitySheet.FileSizeLabelOf(FileSize);
}
