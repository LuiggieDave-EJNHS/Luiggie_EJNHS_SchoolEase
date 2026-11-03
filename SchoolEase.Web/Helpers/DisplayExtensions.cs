using System.ComponentModel.DataAnnotations;
using System.Reflection;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Helpers;

/// <summary>Presentation helpers shared by every view.</summary>
public static class DisplayExtensions
{
    /// <summary>Reads the [Display(Name=...)] attribute, falling back to a split PascalCase label.</summary>
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var display = member?.GetCustomAttribute<DisplayAttribute>();
        if (display?.Name is { Length: > 0 } name) return name;

        return System.Text.RegularExpressions.Regex.Replace(value.ToString(), "(?<!^)([A-Z])", " $1");
    }

    /// <summary>Bootstrap contextual colour for a badge.</summary>
    public static string BootstrapTone(this Enum value) => value switch
    {
        Models.EnrollmentStatus.Enrolled => "success",
        Models.EnrollmentStatus.Pending => "warning",
        Models.EnrollmentStatus.Dropped => "danger",
        Models.EnrollmentStatus.TransferredOut => "secondary",
        Models.EnrollmentStatus.Graduated => "primary",

        Models.EmploymentStatus.Permanent => "success",
        Models.EmploymentStatus.Probationary => "info",
        Models.EmploymentStatus.Contractual => "warning",
        Models.EmploymentStatus.PartTime => "secondary",
        Models.EmploymentStatus.Resigned => "dark",
        Models.EmploymentStatus.Retired => "secondary",

        Models.AttendanceStatus.Present => "success",
        Models.AttendanceStatus.Late => "warning",
        Models.AttendanceStatus.Absent => "danger",
        Models.AttendanceStatus.Excused => "info",

        Models.DocumentRequestStatus.Pending => "warning",
        Models.DocumentRequestStatus.Processing => "info",
        Models.DocumentRequestStatus.ReadyForRelease => "primary",
        Models.DocumentRequestStatus.Released => "success",
        Models.DocumentRequestStatus.Rejected => "danger",

        Models.AnnouncementPriority.Urgent => "danger",
        Models.AnnouncementPriority.High => "warning",
        Models.AnnouncementPriority.Normal => "info",
        Models.AnnouncementPriority.Low => "secondary",

        Models.EventCategory.Holiday => "danger",
        Models.EventCategory.Exam => "warning",
        Models.EventCategory.Meeting => "purple",
        Models.EventCategory.Deadline => "pink",
        Models.EventCategory.Activity => "teal",
        _ => "primary"
    };

    /// <summary>Bootstrap icon class for a badge.</summary>
    public static string Icon(this Enum value) => value switch
    {
        Models.AttendanceStatus.Present => "bi-check-circle",
        Models.AttendanceStatus.Late => "bi-clock-history",
        Models.AttendanceStatus.Absent => "bi-x-circle",
        Models.AttendanceStatus.Excused => "bi-file-earmark-medical",

        Models.DocumentRequestStatus.Pending => "bi-hourglass-split",
        Models.DocumentRequestStatus.Processing => "bi-arrow-repeat",
        Models.DocumentRequestStatus.ReadyForRelease => "bi-box-seam",
        Models.DocumentRequestStatus.Released => "bi-check2-circle",
        Models.DocumentRequestStatus.Rejected => "bi-slash-circle",

        Models.EventCategory.Holiday => "bi-cup-hot",
        Models.EventCategory.Exam => "bi-pencil-square",
        Models.EventCategory.Meeting => "bi-people",
        Models.EventCategory.Deadline => "bi-alarm",
        Models.EventCategory.Activity => "bi-flag",
        _ => "bi-calendar-event"
    };

    public static string GradeRemarks(this double? grade) => grade switch
    {
        null => "Not graded",
        >= 90 => "Outstanding",
        >= 85 => "Very Satisfactory",
        >= 80 => "Satisfactory",
        >= 75 => "Fairly Satisfactory",
        _ => "Failed"
    };

    /// <summary>"3 hours ago" style relative time.</summary>
    public static string ToRelativeTime(this DateTime value)
    {
        var delta = DateTime.UtcNow - value.ToUniversalTime();
        if (delta.TotalSeconds < 0) return "just now";
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} hr ago";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays} day(s) ago";
        if (delta.TotalDays < 30) return $"{(int)(delta.TotalDays / 7)} week(s) ago";
        return value.ToLocalTime().ToString("MMM d, yyyy");
    }

    /// <summary>12-hour clock label.</summary>
    public static string To12Hour(this TimeSpan time) => DateTime.Today.Add(time).ToString("h:mm tt");

    public static string ToDateLabel(this DateTime value) => value.ToString("MMM d, yyyy");

    public static string DayShortName(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun"
    };

    /// <summary>Initials used by the avatar chips.</summary>
    public static string Initials(this string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "?";
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    /// <summary>Deterministic avatar colour derived from the name.</summary>
    public static string AvatarTone(this string? seed)
    {
        string[] tones = { "primary", "success", "warning", "danger", "info", "purple", "teal", "pink" };
        if (string.IsNullOrWhiteSpace(seed)) return tones[0];
        var hash = seed.Sum(c => c);
        return tones[Math.Abs(hash) % tones.Length];
    }

    /// <summary>Formats a school-year string for display.</summary>
    public static string SchoolYearLabel(this string? schoolYear) =>
        string.IsNullOrWhiteSpace(schoolYear) ? SchoolInfo.SchoolYear : schoolYear;
}
