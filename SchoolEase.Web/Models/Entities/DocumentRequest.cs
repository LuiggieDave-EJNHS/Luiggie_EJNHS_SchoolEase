using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A student request for a school document, tracked through the registrar workflow.</summary>
public class DocumentRequest
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    [Display(Name = "Request No.")]
    public string RequestNumber { get; set; } = string.Empty;

    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "Document Type")]
    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    [Required, StringLength(300)]
    [Display(Name = "Purpose")]
    public string Purpose { get; set; } = string.Empty;

    [Range(1, 20)]
    [Display(Name = "No. of Copies")]
    public int Copies { get; set; } = 1;

    [Display(Name = "Status")]
    public DocumentRequestStatus Status { get; set; } = DocumentRequestStatus.Pending;

    [Display(Name = "Requested On")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Processed By")]
    public string? ProcessedByUserId { get; set; }
    public AppUser? ProcessedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    [StringLength(400)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
