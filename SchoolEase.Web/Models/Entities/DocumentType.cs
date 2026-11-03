using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>A document the registrar can issue, with its turnaround time.</summary>
public class DocumentType
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    [Display(Name = "Document Name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }

    [Range(1, 60)]
    [Display(Name = "Processing Days")]
    public int ProcessingDays { get; set; } = 3;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public ICollection<DocumentRequest> Requests { get; set; } = new List<DocumentRequest>();
}
