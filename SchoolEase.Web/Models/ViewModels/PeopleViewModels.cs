using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

/// <summary>Attendance roll-up for one learner.</summary>
public class AttendanceSummary
{
    public int Present { get; set; }
    public int Late { get; set; }
    public int Absent { get; set; }
    public int Excused { get; set; }

    public int Total => Present + Late + Absent + Excused;

    /// <summary>Present plus late over all marks, as a percentage.</summary>
    public double Rate => Total == 0 ? 0 : Math.Round((Present + Late) * 100.0 / Total, 1);
}

/// <summary>Backing model for the student register list screen.</summary>
public class StudentIndexViewModel
{
    public PagedList<Student> Students { get; set; } = new(new List<Student>(), 0, 1, 15);
    public List<StatCard> Stats { get; set; } = new();

    public string? Keyword { get; set; }
    public int? GradeLevelId { get; set; }
    public int? SectionId { get; set; }
    public EnrollmentStatus? Status { get; set; }
    public string? Sort { get; set; }

    public List<SelectListItem> GradeLevels { get; set; } = new();
    public List<SelectListItem> Sections { get; set; } = new();
    public List<SelectListItem> Statuses { get; set; } = new();
}

/// <summary>A self-registered account waiting to be confirmed as a learner.</summary>
public class PendingStudentRegistration
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public DateTime RegisteredAt { get; set; }
}

/// <summary>Backing model for the student verification queue.</summary>
public class StudentVerificationIndexViewModel
{
    public List<PendingStudentRegistration> Pending { get; set; } = new();
}

/// <summary>Full learner profile.</summary>
public class StudentDetailsViewModel
{
    public Student Student { get; set; } = null!;
    public AttendanceSummary Attendance { get; set; } = new();
    public List<GradeRecord> Grades { get; set; } = new();
    public List<DocumentRequest> DocumentRequests { get; set; } = new();
    public List<AttendanceRecord> RecentAttendance { get; set; } = new();

    /// <summary>Scanned records in the learner's 201 file.</summary>
    public List<StudentDocument> Documents { get; set; } = new();
}

/// <summary>Backing model for the staff upload form of a learner's 201 file record.</summary>
public class StudentDocumentFormViewModel
{
    public int StudentId { get; set; }
    public string StudentLabel { get; set; } = string.Empty;

    [Required, StringLength(160)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public StudentDocumentCategory Category { get; set; } = StudentDocumentCategory.Other;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Share with learner")]
    public bool IsVisibleToStudent { get; set; } = true;

    [Display(Name = "File")]
    public IFormFile? File { get; set; }

    public string? ExistingFileName { get; set; }
    public long ExistingFileSize { get; set; }
}

/// <summary>Learner-facing list of the records kept in their own 201 file.</summary>
public class MyDocumentsViewModel
{
    public Student? Student { get; set; }
    public List<StudentDocument> Documents { get; set; } = new();
}

/// <summary>
/// One row in the staff digital-document console. Carries only the display fields,
/// never the file bytes, so browsing the console stays light.
/// </summary>
public class DocumentRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public StudentDocumentCategory Category { get; set; }
    public string? Description { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsVisibleToStudent { get; set; }
    public string SchoolYear { get; set; } = string.Empty;
    public string UploaderName { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public int StudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? Suffix { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string? GradeLevelName { get; set; }
    public string? SectionName { get; set; }

    public string StudentName =>
        string.Join(' ', new[] { FirstName, MiddleName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)))
        + (string.IsNullOrWhiteSpace(Suffix) ? string.Empty : $" {Suffix}");

    public string ClassLabel => GradeLevelName is null
        ? "Unassigned"
        : $"{GradeLevelName} - {(string.IsNullOrWhiteSpace(SectionName) ? "No Section" : SectionName)}";

    public string FileSizeLabel => LearningActivitySheet.FileSizeLabelOf(FileSize);
}

/// <summary>Backing model for the digital document management console.</summary>
public class DocumentManagementIndexViewModel
{
    public PagedList<DocumentRowViewModel> Documents { get; set; } = new(new List<DocumentRowViewModel>(), 0, 1, 15);
    public List<StatCard> Stats { get; set; } = new();

    public string? Keyword { get; set; }
    public StudentDocumentCategory? Category { get; set; }
    public int? GradeLevelId { get; set; }
    public bool? Shared { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
}

/// <summary>Backing model for the teaching personnel list screen.</summary>
public class TeacherIndexViewModel
{
    public PagedList<Teacher> Teachers { get; set; } = new(new List<Teacher>(), 0, 1, 15);
    public List<StatCard> Stats { get; set; } = new();

    public string? Keyword { get; set; }
    public string? Department { get; set; }
    public EmploymentStatus? Status { get; set; }
    public string? Sort { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Statuses { get; set; } = new();
}

/// <summary>Full teaching personnel profile.</summary>
public class TeacherDetailsViewModel
{
    public Teacher Teacher { get; set; } = null!;
    public List<Section> AdvisorySections { get; set; } = new();
    public List<TeacherSubjectAssignment> SubjectAssignments { get; set; } = new();
    public List<ClassSchedule> Schedule { get; set; } = new();
    public double WeeklyHours { get; set; }
}

/// <summary>Backing model for the non-teaching personnel list screen.</summary>
public class StaffIndexViewModel
{
    public PagedList<Staff> Staff { get; set; } = new(new List<Staff>(), 0, 1, 15);
    public List<StatCard> Stats { get; set; } = new();

    public string? Keyword { get; set; }
    public string? Department { get; set; }
    public string? Sort { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
}

/// <summary>Backing model for the guardian list screen.</summary>
public class GuardianIndexViewModel
{
    public PagedList<Guardian> Guardians { get; set; } = new(new List<Guardian>(), 0, 1, 15);
    public List<StatCard> Stats { get; set; } = new();

    public string? Keyword { get; set; }
    public string? Sort { get; set; }
}
