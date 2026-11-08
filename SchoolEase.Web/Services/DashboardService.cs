using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Models.ViewModels;

namespace SchoolEase.Web.Services;

/// <summary>Builds the role-specific dashboards entirely from database queries.</summary>
public interface IDashboardService
{
    Task<DashboardViewModel> BuildAdminDashboardAsync();
    Task<TeacherDashboardViewModel> BuildTeacherDashboardAsync(string userId);
    Task<StudentDashboardViewModel> BuildStudentDashboardAsync(string userId);
}

public class DashboardService : IDashboardService
{
    private static readonly string[] Palette =
    {
        "#800000", "#1aa179", "#f2a93b", "#dc3545", "#6f42c1", "#17a2b8", "#d63384", "#5b6b7c"
    };

    /// <summary>
    /// How long a built dashboard is reused. The pages are read-only summaries, so a
    /// short window turns hundreds of staff refreshes into a few dozen queries while
    /// still showing new announcements and attendance within half a minute.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IMemoryCache _cache;

    public DashboardService(ApplicationDbContext db, IAuditService audit, IMemoryCache cache)
    {
        _db = db;
        _audit = audit;
        _cache = cache;
    }

    // ------------------------------------------------------------------
    // Cached entry points
    // ------------------------------------------------------------------
    public async Task<DashboardViewModel> BuildAdminDashboardAsync() =>
        (await _cache.GetOrCreateAsync("dashboard:admin", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return BuildAdminDashboardCoreAsync();
        }))!;

    public async Task<TeacherDashboardViewModel> BuildTeacherDashboardAsync(string userId) =>
        (await _cache.GetOrCreateAsync($"dashboard:teacher:{userId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return BuildTeacherDashboardCoreAsync(userId);
        }))!;

    public async Task<StudentDashboardViewModel> BuildStudentDashboardAsync(string userId) =>
        (await _cache.GetOrCreateAsync($"dashboard:student:{userId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return BuildStudentDashboardCoreAsync(userId);
        }))!;

    // ------------------------------------------------------------------
    // Administrator dashboard
    // ------------------------------------------------------------------
    private async Task<DashboardViewModel> BuildAdminDashboardCoreAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var now = DateTime.UtcNow;

        var vm = new DashboardViewModel
        {
            TotalStudents = await _db.Students.CountAsync(s => s.IsActive),
            MaleStudents = await _db.Students.CountAsync(s => s.IsActive && s.Gender == Gender.Male),
            FemaleStudents = await _db.Students.CountAsync(s => s.IsActive && s.Gender == Gender.Female),
            TotalTeachers = await _db.Teachers.CountAsync(t => t.IsActive),
            TotalStaff = await _db.StaffMembers.CountAsync(s => s.IsActive),
            TotalSections = await _db.Sections.CountAsync(s => s.IsActive),
            TotalSubjects = await _db.Subjects.CountAsync(s => s.IsActive),
            ActiveEnrollments = await _db.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Enrolled),
            NewStudentsThisMonth = await _db.Students.CountAsync(s => s.DateEnrolled >= monthStart),

            PendingRequests = await _db.DocumentRequests.CountAsync(r => r.Status == DocumentRequestStatus.Pending),
            ReadyForReleaseRequests = await _db.DocumentRequests.CountAsync(r => r.Status == DocumentRequestStatus.ReadyForRelease),
            ReleasedRequests = await _db.DocumentRequests.CountAsync(r => r.Status == DocumentRequestStatus.Released)
        };

        // ----- Today's attendance -----
        // Counted in SQL: at most one row per status instead of every mark of the day.
        var todayCounts = await _db.AttendanceRecords
            .Where(a => a.Date >= today && a.Date < tomorrow)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int TodayCount(AttendanceStatus status) =>
            todayCounts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        vm.PresentToday = TodayCount(AttendanceStatus.Present);
        vm.LateToday = TodayCount(AttendanceStatus.Late);
        vm.AbsentToday = TodayCount(AttendanceStatus.Absent);
        vm.ExcusedToday = TodayCount(AttendanceStatus.Excused);
        vm.AttendanceMarkedToday = todayCounts.Sum(c => c.Count);
        vm.AttendanceRateToday = vm.AttendanceMarkedToday == 0
            ? 0
            : Math.Round((vm.PresentToday + vm.LateToday) * 100.0 / vm.AttendanceMarkedToday, 1);

        // ----- Announcements visible to administrators -----
        vm.Announcements = await _db.Announcements
            .Include(a => a.Author)
            .Where(a => a.IsPublished && a.PublishDate <= now && (a.ExpireDate == null || a.ExpireDate >= now))
            .OrderByDescending(a => a.Priority)
            .ThenByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();

        vm.UpcomingEvents = await _db.SchoolEvents
            .Where(e => e.StartDate >= DateTime.Today)
            .OrderBy(e => e.StartDate)
            .Take(5)
            .ToListAsync();

        vm.RecentStudents = await _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .OrderByDescending(s => s.CreatedAt)
            .Take(6)
            .Select(s => new DashboardStudentRow
            {
                Id = s.Id,
                StudentNumber = s.StudentNumber,
                FullName = s.FirstName + " " + s.LastName,
                GradeSection = s.Section != null ? s.Section.Name : null,
                Status = s.EnrollmentStatus,
                DateEnrolled = s.DateEnrolled
            })
            .ToListAsync();

        // ----- Activity feed from the audit trail -----
        vm.RecentActivities = await BuildActivityFeedAsync(8);

        // ----- Charts -----
        var gradeLevels = await _db.GradeLevels.OrderBy(g => g.OrderIndex).ToListAsync();
        var studentGradeCounts = await _db.Students
            .Where(s => s.IsActive && s.GradeLevelId != null)
            .GroupBy(s => s.GradeLevelId!.Value)
            .Select(g => new { GradeLevelId = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.StudentsByGradeLevel = new ChartData
        {
            Title = "Student Population by Grade Level",
            Labels = gradeLevels.Select(g => g.Name).ToList(),
            Values = gradeLevels.Select(g => (double)(studentGradeCounts.FirstOrDefault(c => c.GradeLevelId == g.Id)?.Count ?? 0)).ToList(),
            Colors = gradeLevels.Select((_, i) => Palette[i % Palette.Length]).ToList()
        };

        var sections = await _db.Sections
            .Include(s => s.GradeLevel)
            .Where(s => s.IsActive)
            .OrderBy(s => s.GradeLevel.OrderIndex)
            .ThenBy(s => s.Name)
            .Select(s => new { s.Id, Label = s.Name })
            .ToListAsync();

        var sectionCounts = await _db.Students
            .Where(s => s.IsActive && s.SectionId != null)
            .GroupBy(s => s.SectionId!.Value)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.StudentsPerSection = new ChartData
        {
            Title = "Students per Section",
            Labels = sections.Select(s => s.Label).ToList(),
            Values = sections.Select(s => (double)(sectionCounts.FirstOrDefault(c => c.SectionId == s.Id)?.Count ?? 0)).ToList(),
            Colors = sections.Select((_, i) => Palette[(i + 2) % Palette.Length]).ToList()
        };

        // Last 7 days that actually have attendance marks
        var trendStart = today.AddDays(-6);

        var trendCounts = await _db.AttendanceRecords
            .Where(a => a.Date >= trendStart && a.Date < tomorrow &&
                        (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .GroupBy(a => a.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var trendLookup = trendCounts.ToDictionary(c => c.Date.Date, c => c.Count);
        var trendDates = Enumerable.Range(0, 7).Select(i => trendStart.AddDays(i)).ToList();

        vm.AttendanceTrend = new ChartData
        {
            Title = "Attendance Trend (Last 7 Days)",
            Labels = trendDates.Select(d => d.ToString("MMM d")).ToList(),
            Values = trendDates.Select(d => (double)trendLookup.GetValueOrDefault(d)).ToList(),
            Colors = trendDates.Select(_ => "#800000").ToList()
        };

        var requestGroups = await _db.DocumentRequests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var requestStatuses = new[]
        {
            DocumentRequestStatus.Pending, DocumentRequestStatus.Processing,
            DocumentRequestStatus.ReadyForRelease, DocumentRequestStatus.Released,
            DocumentRequestStatus.Rejected
        };

        vm.RequestStatusChart = new ChartData
        {
            Title = "Document Requests by Status",
            Labels = requestStatuses.Select(s => s.DisplayName()).ToList(),
            Values = requestStatuses.Select(s => (double)(requestGroups.FirstOrDefault(g => g.Status == s)?.Count ?? 0)).ToList(),
            Colors = new List<string> { "#f2a93b", "#17a2b8", "#800000", "#1aa179", "#dc3545" }
        };

        var enrollmentGroups = await _db.Students
            .GroupBy(s => s.EnrollmentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var enrollmentStatuses = new[]
        {
            EnrollmentStatus.Enrolled, EnrollmentStatus.Pending, EnrollmentStatus.Dropped,
            EnrollmentStatus.TransferredOut, EnrollmentStatus.Graduated
        };

        vm.EnrollmentByStatus = new ChartData
        {
            Title = "Enrollment Status Breakdown",
            Labels = enrollmentStatuses.Select(s => s.DisplayName()).ToList(),
            Values = enrollmentStatuses.Select(s => (double)(enrollmentGroups.FirstOrDefault(g => g.Status == s)?.Count ?? 0)).ToList(),
            Colors = new List<string> { "#1aa179", "#f2a93b", "#dc3545", "#5b6b7c", "#6f42c1" }
        };

        return vm;
    }

    // ------------------------------------------------------------------
    // Teacher dashboard
    // ------------------------------------------------------------------
    private async Task<TeacherDashboardViewModel> BuildTeacherDashboardCoreAsync(string userId)
    {
        var today = DateTime.Today;
        var now = DateTime.UtcNow;

        var teacher = await _db.Teachers
            .Include(t => t.AdvisorySections)
            .FirstOrDefaultAsync(t => t.UserId == userId);

        if (teacher is null)
        {
            return new TeacherDashboardViewModel { TeacherName = "Teacher" };
        }

        var assignments = await _db.TeacherSubjectAssignments
            .Include(a => a.Subject)
            .Include(a => a.Section)
            .Where(a => a.TeacherId == teacher.Id)
            .ToListAsync();

        var sectionIds = assignments.Select(a => a.SectionId)
            .Concat(teacher.AdvisorySections.Select(s => s.Id))
            .Distinct()
            .ToList();

        var vm = new TeacherDashboardViewModel
        {
            TeacherName = teacher.FullName,
            AdvisorySectionCount = teacher.AdvisorySections.Count,
            AssignedSectionCount = sectionIds.Count,
            AssignedSubjectCount = assignments.Select(a => a.SubjectId).Distinct().Count(),
            StudentsHandled = await _db.Students.CountAsync(s => s.IsActive && s.SectionId != null && sectionIds.Contains(s.SectionId.Value)),
            ClassesToday = await _db.ClassSchedules.CountAsync(c => c.TeacherId == teacher.Id && c.DayOfWeek == today.DayOfWeek)
        };

        vm.TodaySchedule = (await _db.ClassSchedules
            .Include(c => c.Subject)
            .Include(c => c.Section)
            .Where(c => c.TeacherId == teacher.Id && c.DayOfWeek == today.DayOfWeek)
            .ToListAsync())
            .OrderBy(c => c.StartTime)
            .ToList();

        var markedToday = await _db.AttendanceRecords
            .Where(a => a.Date >= today && a.Date < today.AddDays(1) &&
                        sectionIds.Contains(a.SectionId) &&
                        a.RecordedByUserId == userId)
            .Select(a => a.SectionId)
            .Distinct()
            .ToListAsync();

        vm.AttendancePendingToday = Math.Max(0, vm.ClassesToday - markedToday.Count);

        var thisMonth = new DateTime(today.Year, today.Month, 1);
        vm.GradesPendingEncoding = await _db.GradeRecords
            .CountAsync(g => g.TeacherId == teacher.Id && g.FinalGrade == null && g.EncodedAt >= thisMonth);

        vm.Announcements = await _db.Announcements
            .Where(a => a.IsPublished && (a.Audience == Audience.All || a.Audience == Audience.Teachers) &&
                        a.PublishDate <= now && (a.ExpireDate == null || a.ExpireDate >= now))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();

        vm.UpcomingEvents = await _db.SchoolEvents
            .Where(e => e.StartDate >= DateTime.Today)
            .OrderBy(e => e.StartDate)
            .Take(5)
            .ToListAsync();

        var trendStart = today.AddDays(-6);
        var dayAfterToday = today.AddDays(1);

        var marks = await _db.AttendanceRecords
            .Where(a => a.Date >= trendStart && a.Date < dayAfterToday &&
                        sectionIds.Contains(a.SectionId) &&
                        (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .GroupBy(a => a.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var markLookup = marks.ToDictionary(m => m.Date.Date, m => m.Count);
        var dates = Enumerable.Range(0, 7).Select(i => trendStart.AddDays(i)).ToList();

        vm.AttendanceTrend = new ChartData
        {
            Title = "My Classes Attendance (Last 7 Days)",
            Labels = dates.Select(d => d.ToString("MMM d")).ToList(),
            Values = dates.Select(d => (double)markLookup.GetValueOrDefault(d)).ToList(),
            Colors = dates.Select(_ => "#1aa179").ToList()
        };

        var sectionCounts = await _db.Students
            .Where(s => s.IsActive && s.SectionId != null && sectionIds.Contains(s.SectionId.Value))
            .GroupBy(s => s.SectionId!.Value)
            .Select(g => new { SectionId = g.Key, Count = g.Count() })
            .ToListAsync();

        var sectionLookup = assignments.Select(a => new { a.SectionId, a.Section.Name })
            .Concat(teacher.AdvisorySections.Select(s => new { SectionId = s.Id, s.Name }))
            .DistinctBy(x => x.SectionId)
            .ToList();

        vm.SectionPopulation = new ChartData
        {
            Title = "Students per Assigned Section",
            Labels = sectionLookup.Select(s => s.Name).ToList(),
            Values = sectionLookup.Select(s => (double)(sectionCounts.FirstOrDefault(c => c.SectionId == s.SectionId)?.Count ?? 0)).ToList(),
            Colors = sectionLookup.Select((_, i) => Palette[i % Palette.Length]).ToList()
        };

        return vm;
    }

    // ------------------------------------------------------------------
    // Student dashboard
    // ------------------------------------------------------------------
    private async Task<StudentDashboardViewModel> BuildStudentDashboardCoreAsync(string userId)
    {
        var today = DateTime.Today;
        var now = DateTime.UtcNow;

        var student = await _db.Students
            .Include(s => s.GradeLevel)
            .Include(s => s.Section)
            .Include(s => s.Guardian)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student is null)
        {
            return new StudentDashboardViewModel { StudentName = "Student" };
        }

        var vm = new StudentDashboardViewModel
        {
            Student = student,
            StudentName = student.FullName
        };

        var grades = await _db.GradeRecords
            .Include(g => g.Subject)
            .Where(g => g.StudentId == student.Id)
            .OrderByDescending(g => g.Term)
            .ToListAsync();

        vm.LatestGrades = grades.Take(6).ToList();
        vm.GeneralAverage = grades.Where(g => g.FinalGrade.HasValue).Select(g => g.FinalGrade!.Value)
            .DefaultIfEmpty()
            .Average();

        // Counted in SQL so a learner's whole attendance history is never loaded.
        var attendanceCounts = await _db.AttendanceRecords
            .Where(a => a.StudentId == student.Id)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int AttendanceCount(AttendanceStatus status) =>
            attendanceCounts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        vm.PresentDays = AttendanceCount(AttendanceStatus.Present);
        vm.LateDays = AttendanceCount(AttendanceStatus.Late);
        vm.AbsentDays = AttendanceCount(AttendanceStatus.Absent);

        var markedDays = attendanceCounts.Sum(c => c.Count);
        vm.AttendanceRate = markedDays == 0
            ? 0
            : Math.Round((vm.PresentDays + vm.LateDays) * 100.0 / markedDays, 1);

        var requests = await _db.DocumentRequests
            .Include(r => r.DocumentType)
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        vm.RecentRequests = requests.Take(5).ToList();
        vm.TotalRequests = requests.Count;
        vm.PendingRequests = requests.Count(r => r.Status is DocumentRequestStatus.Pending or DocumentRequestStatus.Processing);

        if (student.SectionId is not null)
        {
            var schedule = (await _db.ClassSchedules
                .Include(c => c.Subject)
                .Include(c => c.Teacher)
                .Include(c => c.Section)
                .Where(c => c.SectionId == student.SectionId)
                .ToListAsync())
                .OrderBy(c => c.DayOfWeek)
                .ThenBy(c => c.StartTime)
                .ToList();

            vm.WeeklySchedule = schedule;
            vm.TodaySchedule = schedule.Where(c => c.DayOfWeek == today.DayOfWeek).ToList();
        }

        vm.Announcements = await _db.Announcements
            .Where(a => a.IsPublished && (a.Audience == Audience.All || a.Audience == Audience.Students) &&
                        a.PublishDate <= now && (a.ExpireDate == null || a.ExpireDate >= now))
            .OrderByDescending(a => a.PublishDate)
            .Take(5)
            .ToListAsync();

        vm.UpcomingEvents = await _db.SchoolEvents
            .Where(e => e.StartDate >= DateTime.Today &&
                        (e.Audience == Audience.All || e.Audience == Audience.Students))
            .OrderBy(e => e.StartDate)
            .Take(5)
            .ToListAsync();

        var trendStart = today.AddDays(-6);

        var recentCounts = await _db.AttendanceRecords
            .Where(a => a.StudentId == student.Id && a.Date >= trendStart &&
                        (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .GroupBy(a => a.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var recentLookup = recentCounts.ToDictionary(c => c.Date.Date, c => c.Count);
        var dates = Enumerable.Range(0, 7).Select(i => trendStart.AddDays(i)).ToList();

        vm.AttendanceTrend = new ChartData
        {
            Title = "My Attendance (Last 7 Days)",
            Labels = dates.Select(d => d.ToString("MMM d")).ToList(),
            Values = dates.Select(d => (double)recentLookup.GetValueOrDefault(d)).ToList(),
            Colors = dates.Select(_ => "#800000").ToList()
        };

        return vm;
    }

    // ------------------------------------------------------------------
    private async Task<List<ActivityItem>> BuildActivityFeedAsync(int count)
    {
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();

        return logs.Select(l => new ActivityItem
        {
            Title = string.IsNullOrWhiteSpace(l.UserName) ? l.Action : $"{l.UserName} — {l.Action}",
            Description = l.Details ?? (l.EntityName is null ? null : $"{l.EntityName} #{l.EntityId}"),
            Timestamp = l.CreatedAt,
            Tone = l.Action.Contains("Delete", StringComparison.OrdinalIgnoreCase) ? "danger"
                : l.Action.Contains("Create", StringComparison.OrdinalIgnoreCase) || l.Action.Contains("Add", StringComparison.OrdinalIgnoreCase) ? "success"
                : l.Action.Contains("Update", StringComparison.OrdinalIgnoreCase) ? "warning"
                : "primary",
            Icon = l.Action.Contains("Login", StringComparison.OrdinalIgnoreCase) ? "bi-box-arrow-in-right"
                : l.Action.Contains("Delete", StringComparison.OrdinalIgnoreCase) ? "bi-trash"
                : l.Action.Contains("Create", StringComparison.OrdinalIgnoreCase) ? "bi-plus-circle"
                : l.Action.Contains("Update", StringComparison.OrdinalIgnoreCase) ? "bi-pencil"
                : "bi-activity"
        }).ToList();
    }
}
