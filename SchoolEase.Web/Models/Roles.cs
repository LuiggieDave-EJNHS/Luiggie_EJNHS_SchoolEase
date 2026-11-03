namespace SchoolEase.Web.Models;

/// <summary>
/// Application role names used across the system. Stored verbatim in the Identity role table.
/// </summary>
public static class Roles
{
    public const string SuperAdministrator = "Super Administrator";
    public const string SchoolAdministrator = "School Administrator";
    public const string IctHeadCoordinator = "ICT Head Coordinator";
    public const string Teacher = "Teacher";
    public const string Registrar = "Registrar";
    public const string Student = "Student";
    public const string FormerStudent = "Former Student";

    /// <summary>All roles in descending order of privilege.</summary>
    public static readonly string[] All =
    {
        SuperAdministrator,
        SchoolAdministrator,
        IctHeadCoordinator,
        Teacher,
        Registrar,
        Student,
        FormerStudent
    };

    /// <summary>Roles that may reach the full administrator control panel.</summary>
    public static readonly string[] Administrators = { SuperAdministrator, SchoolAdministrator };

    /// <summary>
    /// Roles that may set up and oversee academics (classes, subjects, schedules, teachers).
    /// </summary>
    public static readonly string[] AcademicOversight = { SuperAdministrator, SchoolAdministrator, IctHeadCoordinator };

    /// <summary>Roles allowed to encode attendance and grades.</summary>
    public static readonly string[] TeachingStaff = { Teacher };

    /// <summary>Anyone with a staff-level back-office role.</summary>
    public static readonly string[] NonStudent = { SuperAdministrator, SchoolAdministrator, IctHeadCoordinator, Teacher, Registrar };
}

/// <summary>Authorization policy names registered in Program.cs.</summary>
public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string StaffOnly = "StaffOnly";
    public const string TeacherOrAdmin = "TeacherOrAdmin";
    public const string CanPostAnnouncements = "CanPostAnnouncements";
    public const string CanManageRequests = "CanManageRequests";
    public const string AcademicOversight = "AcademicOversight";
}
