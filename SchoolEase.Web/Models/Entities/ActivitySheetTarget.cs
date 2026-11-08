using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// Sends a Learning Activity Sheet to one class section. A sheet with no targets
/// is treated as school-wide; once it has targets, only those sections see it.
/// </summary>
public class ActivitySheetTarget
{
    public int Id { get; set; }

    public int ActivitySheetId { get; set; }
    public LearningActivitySheet? ActivitySheet { get; set; }

    [Display(Name = "Section")]
    public int SectionId { get; set; }
    public Section? Section { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
