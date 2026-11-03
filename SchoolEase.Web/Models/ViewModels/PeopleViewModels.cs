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

/// <summary>Full learner profile.</summary>
public class StudentDetailsViewModel
{
    public Student Student { get; set; } = null!;
    public AttendanceSummary Attendance { get; set; } = new();
    public List<GradeRecord> Grades { get; set; } = new();
    public List<DocumentRequest> DocumentRequests { get; set; } = new();
    public List<AttendanceRecord> RecentAttendance { get; set; } = new();
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
