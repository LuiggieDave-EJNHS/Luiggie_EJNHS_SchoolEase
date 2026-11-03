using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

/// <summary>Simple label/value pair consumed by Chart.js.</summary>
public class ChartData
{
    public string Title { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<double> Values { get; set; } = new();
    public List<string> Colors { get; set; } = new();

    public bool IsEmpty => Values.Count == 0 || Values.All(v => v == 0);
    public double Max => Values.Count == 0 ? 0 : Values.Max();
}

/// <summary>One row of the dashboard activity feed.</summary>
public class ActivityItem
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = "bi-activity";
    public string Tone { get; set; } = "primary";
    public DateTime Timestamp { get; set; }
    public string? Url { get; set; }
}

/// <summary>Everything the landing dashboard needs, all of it read from the database.</summary>
public class DashboardViewModel
{
    public string ScopeLabel { get; set; } = "School-wide";

    // Counters
    public int TotalStudents { get; set; }
    public int MaleStudents { get; set; }
    public int FemaleStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalStaff { get; set; }
    public int TotalSections { get; set; }
    public int TotalSubjects { get; set; }
    public int ActiveEnrollments { get; set; }
    public int NewStudentsThisMonth { get; set; }

    // Attendance (today)
    public int PresentToday { get; set; }
    public int LateToday { get; set; }
    public int AbsentToday { get; set; }
    public int ExcusedToday { get; set; }
    public int AttendanceMarkedToday { get; set; }
    public double AttendanceRateToday { get; set; }

    // Workflow
    public int PendingRequests { get; set; }
    public int ReadyForReleaseRequests { get; set; }
    public int ReleasedRequests { get; set; }

    // Lists
    public List<Announcement> Announcements { get; set; } = new();
    public List<ActivityItem> RecentActivities { get; set; } = new();
    public List<SchoolEvent> UpcomingEvents { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public List<DashboardStudentRow> RecentStudents { get; set; } = new();

    // Charts
    public ChartData StudentsByGradeLevel { get; set; } = new() { Title = "Student Population by Grade Level" };
    public ChartData StudentsPerSection { get; set; } = new() { Title = "Students per Section" };
    public ChartData AttendanceTrend { get; set; } = new() { Title = "Attendance Trend (Last 7 School Days)" };
    public ChartData RequestStatusChart { get; set; } = new() { Title = "Document Requests by Status" };
    public ChartData EnrollmentByStatus { get; set; } = new() { Title = "Enrollment Status Breakdown" };

    public bool HasAnyStudents => TotalStudents > 0;
}

public class DashboardStudentRow
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? GradeSection { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTime DateEnrolled { get; set; }
}

/// <summary>Teacher-facing dashboard counters.</summary>
public class TeacherDashboardViewModel
{
    public string TeacherName { get; set; } = string.Empty;
    public int AdvisorySectionCount { get; set; }
    public int AssignedSectionCount { get; set; }
    public int AssignedSubjectCount { get; set; }
    public int StudentsHandled { get; set; }
    public int ClassesToday { get; set; }
    public int AttendancePendingToday { get; set; }
    public int GradesPendingEncoding { get; set; }

    public List<ClassSchedule> TodaySchedule { get; set; } = new();
    public List<Announcement> Announcements { get; set; } = new();
    public List<SchoolEvent> UpcomingEvents { get; set; } = new();
    public ChartData AttendanceTrend { get; set; } = new() { Title = "My Class Attendance (Last 7 School Days)" };
    public ChartData SectionPopulation { get; set; } = new() { Title = "Students per Assigned Section" };
}

/// <summary>Student-facing dashboard.</summary>
public class StudentDashboardViewModel
{
    public Student? Student { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public double? GeneralAverage { get; set; }
    public int PresentDays { get; set; }
    public int LateDays { get; set; }
    public int AbsentDays { get; set; }
    public double AttendanceRate { get; set; }
    public int PendingRequests { get; set; }
    public int TotalRequests { get; set; }

    public List<ClassSchedule> TodaySchedule { get; set; } = new();
    public List<ClassSchedule> WeeklySchedule { get; set; } = new();
    public List<Announcement> Announcements { get; set; } = new();
    public List<SchoolEvent> UpcomingEvents { get; set; } = new();
    public List<GradeRecord> LatestGrades { get; set; } = new();
    public List<DocumentRequest> RecentRequests { get; set; } = new();
    public ChartData AttendanceTrend { get; set; } = new() { Title = "My Attendance (Last 7 School Days)" };
}
