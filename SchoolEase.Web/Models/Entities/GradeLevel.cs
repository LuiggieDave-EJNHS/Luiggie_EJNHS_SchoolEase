using System.ComponentModel.DataAnnotations;

namespace SchoolEase.Web.Models.Entities;

/// <summary>Grade 11 / Grade 12 (or lower levels if the school expands).</summary>
public class GradeLevel
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    [Display(Name = "Grade Level")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(10)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "Sort Order")]
    public int OrderIndex { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public ICollection<Section> Sections { get; set; } = new List<Section>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
