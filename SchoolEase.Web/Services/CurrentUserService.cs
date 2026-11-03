using System.Security.Claims;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Services;

/// <summary>Read-only access to the authenticated principal for the current request.</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string FullName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool IsAdministrator { get; }
    bool IsSuperAdministrator { get; }
    bool IsIctHeadCoordinator { get; }
    bool IsTeacher { get; }
    bool IsStudent { get; }
    bool IsFormerStudent { get; }
    bool IsRegistrar { get; }

    /// <summary>True for roles that see the whole school instead of only their own classes.</summary>
    bool HasSchoolWideAccess { get; }

    /// <summary>Highest-privilege role held by the user (used for sidebar selection).</summary>
    string PrimaryRole { get; }

    /// <summary>Landing page for the current role.</summary>
    string HomeAction { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Principal?.Identity?.Name;

    public string FullName =>
        Principal?.FindFirstValue("FullName")
        ?? Principal?.FindFirstValue(ClaimTypes.GivenName)
        ?? Principal?.Identity?.Name
        ?? "Guest";

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool IsAdministrator => IsInRole(Roles.SuperAdministrator) || IsInRole(Roles.SchoolAdministrator);
    public bool IsSuperAdministrator => IsInRole(Roles.SuperAdministrator);
    public bool IsIctHeadCoordinator => IsInRole(Roles.IctHeadCoordinator);
    public bool IsTeacher => IsInRole(Roles.Teacher);
    public bool IsStudent => IsInRole(Roles.Student) || IsInRole(Roles.FormerStudent);
    public bool IsFormerStudent => IsInRole(Roles.FormerStudent);
    public bool IsRegistrar => IsInRole(Roles.Registrar);

    public bool HasSchoolWideAccess => IsAdministrator || IsIctHeadCoordinator;

    public string PrimaryRole
    {
        get
        {
            foreach (var role in Roles.All)
            {
                if (IsInRole(role)) return role;
            }

            return string.Empty;
        }
    }

    public string HomeAction => PrimaryRole switch
    {
        Roles.Student => "/Student/Dashboard",
        Roles.FormerStudent => "/Student/Dashboard",
        Roles.Teacher => "/Teacher/Dashboard",
        _ => "/Dashboard/Index"
    };
}
