using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using SchoolEase.Web.Data;
using SchoolEase.Web.Helpers;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Models.ViewModels;

// ======================================================================
// Document Requests
// ======================================================================

/// <summary>Registrar worklist: filtered, paged document requests plus summary tiles.</summary>
public class DocumentRequestListViewModel
{
    public PagedList<DocumentRequest> Requests { get; set; } = new(new List<DocumentRequest>(), 0, 1, 15);
    public DocumentRequestStatus? Status { get; set; }
    public string? Keyword { get; set; }
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int ReadyForRelease { get; set; }
    public int Released { get; set; }
    public ChartData StatusChart { get; set; } = new();
    public bool IsStaffView { get; set; }
}

/// <summary>Full request view with the status timeline and processing controls.</summary>
public class DocumentRequestDetailsViewModel
{
    public DocumentRequest Request { get; set; } = null!;
    public bool CanManage { get; set; }
    public List<TimelineStep> Timeline { get; set; } = new();
}

/// <summary>One node on the request workflow timeline.</summary>
public class TimelineStep
{
    public string Label { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime? Timestamp { get; set; }
    public bool IsComplete { get; set; }
    public bool IsCurrent { get; set; }
}

/// <summary>Backing model for the student document-request form.</summary>
public class DocumentRequestCreateViewModel
{
    public List<DocumentType> DocumentTypes { get; set; } = new();

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a document type.")]
    [Display(Name = "Document Type")]
    public int DocumentTypeId { get; set; }

    [Required, StringLength(300)]
    [Display(Name = "Purpose")]
    public string Purpose { get; set; } = string.Empty;

    [Range(1, 20)]
    [Display(Name = "No. of Copies")]
    public int Copies { get; set; } = 1;
}

/// <summary>Document-type management table row carrying its usage count.</summary>
public class DocumentTypeRow
{
    public DocumentType Type { get; set; } = null!;
    public int RequestCount { get; set; }
}

public class DocumentTypesViewModel
{
    public List<DocumentTypeRow> Types { get; set; } = new();
}

// ======================================================================
// Announcements
// ======================================================================

public class AnnouncementListViewModel
{
    public PagedList<Announcement> Announcements { get; set; } = new(new List<Announcement>(), 0, 1, 10);
    public AnnouncementPriority? Priority { get; set; }
    public Audience? Audience { get; set; }
    public string? Keyword { get; set; }
    public bool CanManage { get; set; }
}

// ======================================================================
// Calendar
// ======================================================================

public class CalendarDay
{
    public DateTime? Date { get; set; }
    public int DayNumber { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public List<SchoolEvent> Events { get; set; } = new();
}

public class CalendarIndexViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty;
    public int PreviousYear { get; set; }
    public int PreviousMonth { get; set; }
    public int NextYear { get; set; }
    public int NextMonth { get; set; }
    public List<CalendarDay> Days { get; set; } = new();
    public List<SchoolEvent> MonthEvents { get; set; } = new();
    public List<SchoolEvent> Upcoming { get; set; } = new();
    public bool CanManage { get; set; }
}

// ======================================================================
// System Settings
// ======================================================================

public class SystemSummaryViewModel
{
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string AspNetCoreVersion { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
    public int Students { get; set; }
    public int Teachers { get; set; }
    public int Staff { get; set; }
    public int Sections { get; set; }
    public int Subjects { get; set; }
    public int Users { get; set; }
}

public class SettingsIndexViewModel
{
    public List<SystemSetting> Settings { get; set; } = new();
    public SystemSummaryViewModel Summary { get; set; } = new();

    /// <summary>True when the signed-in Super Administrator has an active recovery code.</summary>
    public bool HasRecoveryCode { get; set; }

    /// <summary>When the current recovery code was generated, if any.</summary>
    public string? RecoveryCodeCreatedAt { get; set; }

    /// <summary>Demo rows a cleanup would remove. Only populated for a Super Administrator.</summary>
    public Data.DemoPurgeReport? DemoData { get; set; }

    /// <summary>Settings grouped by their <see cref="SystemSetting.Group"/> value.</summary>
    public IEnumerable<IGrouping<string, SystemSetting>> Groups =>
        Settings.GroupBy(s => s.Group);
}

// ======================================================================
// Audit Logs
// ======================================================================

public class AuditLogIndexViewModel
{
    public PagedList<AuditLog> Logs { get; set; } = new(new List<AuditLog>(), 0, 1, 25);
    public string? Keyword { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public int Total { get; set; }
    public int Today { get; set; }
    public int LoginsToday { get; set; }
    public int FailedLoginsToday { get; set; }
    public int DeletionsToday { get; set; }
    public List<string> Actions { get; set; } = new();
    public List<string> Modules { get; set; } = new();
}

// ======================================================================
// Accounts
// ======================================================================

/// <summary>One account row with the resolved role names.</summary>
public class AccountRow
{
    public AppUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
}

public class AccountListViewModel
{
    public List<AccountRow> Accounts { get; set; } = new();
    public string? Keyword { get; set; }
    public string? Role { get; set; }
    public bool? Active { get; set; }
}

public class AccountDetailsViewModel
{
    public AppUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
    public Student? Student { get; set; }
    public Teacher? Teacher { get; set; }
    public Staff? Staff { get; set; }
    public List<AuditLog> RecentLogs { get; set; } = new();
}

/// <summary>Create-account form (kept separate from <c>AccountViewModels.cs</c>).</summary>
public class AccountCreateViewModel
{
    [Required, StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[^a-zA-Z0-9]).*$", ErrorMessage = "Password must include at least one special character (for example !, @, # or $).")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Edit-account form with simple role reassignment.</summary>
public class AccountEditViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(120)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(120)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Password reset form used on the account details page.</summary>
public class AccountResetPasswordViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "The new password must be at least 8 characters.")]
    [RegularExpression(@"^(?=.*[^a-zA-Z0-9]).*$", ErrorMessage = "The new password must include at least one special character.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;
}

// ======================================================================
// Learning Activity Sheets (LAS)
// ======================================================================

/// <summary>Filter state, summary tiles and the paged library listing.</summary>
public class ActivitySheetListViewModel
{
    public PagedList<LearningActivitySheet> Sheets { get; set; } = new(new List<LearningActivitySheet>(), 0, 1, 12);

    public int? SubjectId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? Term { get; set; }
    public string? Keyword { get; set; }

    public List<SubjectOption> Subjects { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();

    /// <summary>True when the signed-in user may upload and manage sheets (teachers).</summary>
    public bool CanUpload { get; set; }
    public bool IsFiltered { get; set; }

    public int TotalSheets { get; set; }
    public int PublishedSheets { get; set; }
    public int DraftSheets { get; set; }
    public int TotalDownloads { get; set; }
}

/// <summary>Backing model for uploading or editing an activity sheet.</summary>
public class ActivitySheetFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(160, MinimumLength = 3)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please choose a subject.")]
    [Display(Name = "Subject")]
    public int SubjectId { get; set; }

    [Display(Name = "Grade Level")]
    public int? GradeLevelId { get; set; }

    [Range(1, 3, ErrorMessage = "Please choose a term.")]
    [Display(Name = "Term")]
    public int Term { get; set; } = 1;

    [Range(1, 20, ErrorMessage = "Week must be between 1 and 20.")]
    [Display(Name = "Week")]
    public int? WeekNumber { get; set; }

    [StringLength(40)]
    [Display(Name = "MELC Code")]
    public string? MelcCode { get; set; }

    [StringLength(400)]
    [Display(Name = "Competency")]
    public string? Competency { get; set; }

    [Display(Name = "Publish to the library")]
    public bool IsPublished { get; set; } = true;

    [Display(Name = "Activity sheet file")]
    public IFormFile? File { get; set; }

    /// <summary>Name of the file already stored, shown while editing.</summary>
    public string? ExistingFileName { get; set; }

    /// <summary>Size of the file already stored, shown while editing.</summary>
    public long ExistingFileSize { get; set; }

    public List<SubjectOption> Subjects { get; set; } = new();
    public List<SelectListItem> GradeLevels { get; set; } = new();
}

/// <summary>One section in the "send to sections" picker.</summary>
public class SectionCheckOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? TrackStrand { get; set; }
    public int? GradeLevelId { get; set; }
    public string GradeLevelName { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>"12-ICT-CCS • ICT" style label.</summary>
    public string Label => string.IsNullOrWhiteSpace(TrackStrand) ? Name : $"{Name} • {TrackStrand}";
}

/// <summary>Backing model for sending an activity sheet to specific sections.</summary>
public class ActivitySheetSendViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SubjectLabel { get; set; }

    /// <summary>Grade level filter; also the default scope offered to the teacher.</summary>
    public int? GradeLevelId { get; set; }

    public List<SelectListItem> GradeLevels { get; set; } = new();
    public List<SectionCheckOption> Sections { get; set; } = new();

    public int SelectedCount => Sections.Count(s => s.IsSelected);
}
