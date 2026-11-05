using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Services;

/// <summary>
/// Manages the single-use recovery code that lets the Super Administrator unlock
/// their own account after a lockout. The code is only ever stored as a password
/// hash in the Identity user-token store, so the plain text is never persisted.
/// </summary>
public static class RecoveryCodes
{
    /// <summary>Identity token provider; keeps the code out of the editable settings tables.</summary>
    private const string TokenProvider = "SchoolEase.Recovery";

    private const string TokenName = "SuperAdminRecoveryCode";

    private const string CreatedTokenName = "SuperAdminRecoveryCodeCreatedAt";

    // Ambiguous characters (0/O, 1/I) are omitted so the code is easy to read back.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly PasswordHasher<AppUser> Hasher = new();

    /// <summary>Creates a new code in the format <c>XXXX-XXXX-XXXX-XXXX-XXXX</c>.</summary>
    public static string Generate()
    {
        var groups = new string[5];
        for (var g = 0; g < groups.Length; g++)
        {
            var chars = new char[4];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            groups[g] = new string(chars);
        }

        return string.Join('-', groups);
    }

    /// <summary>Uppercases the code and drops anything that is not a letter or digit.</summary>
    public static string Normalize(string? code) =>
        new((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static Task<string?> GetHashAsync(UserManager<AppUser> users, AppUser user) =>
        users.GetAuthenticationTokenAsync(user, TokenProvider, TokenName);

    public static Task<string?> GetCreatedAtAsync(UserManager<AppUser> users, AppUser user) =>
        users.GetAuthenticationTokenAsync(user, TokenProvider, CreatedTokenName);

    /// <summary>Stores the hash of <paramref name="plainCode"/> together with the creation date.</summary>
    public static async Task StoreAsync(UserManager<AppUser> users, AppUser user, string plainCode)
    {
        await users.SetAuthenticationTokenAsync(
            user, TokenProvider, TokenName, Hasher.HashPassword(user, Normalize(plainCode)));
        await users.SetAuthenticationTokenAsync(
            user, TokenProvider, CreatedTokenName, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
    }

    public static async Task ClearAsync(UserManager<AppUser> users, AppUser user)
    {
        await users.RemoveAuthenticationTokenAsync(user, TokenProvider, TokenName);
        await users.RemoveAuthenticationTokenAsync(user, TokenProvider, CreatedTokenName);
    }

    /// <summary>True when <paramref name="plainCode"/> matches the stored hash.</summary>
    public static async Task<bool> MatchesAsync(UserManager<AppUser> users, AppUser user, string? plainCode)
    {
        var hash = await GetHashAsync(users, user);
        if (string.IsNullOrEmpty(hash) || string.IsNullOrWhiteSpace(plainCode)) return false;

        return Hasher.VerifyHashedPassword(user, hash, Normalize(plainCode)) != PasswordVerificationResult.Failed;
    }
}
