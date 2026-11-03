using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolEase.Web.Models.Entities;

/// <summary>
/// A term academic record for a student in a subject.
/// Grading follows the DepEd SHS weighting: 30% written work, 50% performance task, 20% term assessment.
/// </summary>
public class GradeRecord
{
    public const double WrittenWorkWeight = 0.30;
    public const double PerformanceTaskWeight = 0.50;
    public const double TermAssessmentWeight = 0.20;

    public int Id { get; set; }

    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Display(Name = "Subject")]
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    [Display(Name = "Section")]
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    [Display(Name = "Teacher")]
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    [Required, StringLength(20)]
    [Display(Name = "School Year")]
    public string SchoolYear { get; set; } = SchoolInfo.SchoolYear;

    [Display(Name = "Term")]
    public int Term { get; set; } = 1;

    [Range(0, 100)]
    [Display(Name = "Written Work (30%)")]
    public double? WrittenWork { get; set; }

    [Range(0, 100)]
    [Display(Name = "Performance Task (50%)")]
    public double? PerformanceTask { get; set; }

    [Range(0, 100)]
    [Display(Name = "Term Assessment (20%)")]
    public double? TermAssessment { get; set; }

    /// <summary>Weighted term grade (auto-computed when blank).</summary>
    [Range(0, 100)]
    [Display(Name = "Final Grade")]
    public double? FinalGrade { get; set; }

    [StringLength(200)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    [Display(Name = "Encoded By")]
    public string? EncodedByUserId { get; set; }
    public AppUser? EncodedBy { get; set; }

    public DateTime EncodedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Computes the weighted grade from the three components.</summary>
    public double ComputeWeighted()
    {
        var ww = WrittenWork ?? 0;
        var pt = PerformanceTask ?? 0;
        var qa = TermAssessment ?? 0;
        return Math.Round((ww * WrittenWorkWeight) + (pt * PerformanceTaskWeight) + (qa * TermAssessmentWeight), 2);
    }

    [NotMapped]
    public string Descriptor => FinalGrade switch
    {
        null => "Not yet graded",
        >= 90 => "Outstanding",
        >= 85 => "Very Satisfactory",
        >= 80 => "Satisfactory",
        >= 75 => "Fairly Satisfactory",
        _ => "Did Not Meet Expectations"
    };

    [NotMapped]
    public bool IsPassed => FinalGrade >= 75;
}
