using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Services;

/// <summary>
/// Adds the user's display name to the authentication cookie so layouts and
/// audit entries never need an extra database round-trip.
/// </summary>
public class SchoolEaseClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public SchoolEaseClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim("FullName", user.FullName));
        identity.AddClaim(new Claim(ClaimsIdentity.DefaultNameClaimType, user.UserName ?? user.Email ?? user.Id));

        return identity;
    }
}
