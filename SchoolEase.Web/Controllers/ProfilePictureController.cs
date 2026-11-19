using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Controllers;

/// <summary>
/// Serves and manages the signed-in user's profile photo. Any authenticated user may
/// set, replace or clear their own photo; photos are readable by signed-in users so
/// they can be shown next to a name.
/// </summary>
[Authorize]
public class ProfilePictureController : Controller
{
    private const long MaxBytes = 2 * 1024 * 1024; // 2 MB

    private readonly IProfilePhotoService _photos;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public ProfilePictureController(
        IProfilePhotoService photos,
        ICurrentUserService currentUser,
        IAuditService audit)
    {
        _photos = photos;
        _currentUser = currentUser;
        _audit = audit;
    }

    // ------------------------------------------------------------------ Serve
    public async Task<IActionResult> Avatar(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return NotFound();

        var photo = await _photos.GetAsync(id);
        if (photo is null || photo.Content.Length == 0) return NotFound();

        Response.Headers["Cache-Control"] = "private, max-age=300";
        return File(photo.Content, photo.ContentType);
    }

    // ----------------------------------------------------------------- Upload
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? photo, string? returnUrl)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Challenge();

        var (bytes, contentType, error) = await ReadPhotoAsync(photo);
        if (error is not null)
        {
            TempData["Error"] = error;
            return Redirect(ReturnTo(returnUrl));
        }

        await _photos.SaveAsync(userId, bytes!, contentType!);
        await _audit.LogAsync("Update Profile Photo", "AppUser", userId, "Profile photo updated.");

        TempData["Success"] = "Your profile photo has been updated.";
        return Redirect(ReturnTo(returnUrl));
    }

    // ----------------------------------------------------------------- Remove
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string? returnUrl)
    {
        var userId = _currentUser.UserId;
        if (userId is null) return Challenge();

        await _photos.RemoveAsync(userId);
        await _audit.LogAsync("Remove Profile Photo", "AppUser", userId, "Profile photo removed.");

        TempData["Success"] = "Your profile photo has been removed.";
        return Redirect(ReturnTo(returnUrl));
    }

    // ----------------------------------------------------------------- Helpers
    private string ReturnTo(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl! : _currentUser.HomeAction;

    /// <summary>
    /// Validates the upload and reads it into memory. The content type is derived from
    /// the extension rather than trusted from the browser, so only raster images can
    /// be stored (no SVG or other scriptable formats).
    /// </summary>
    private static async Task<(byte[]? Bytes, string? ContentType, string? Error)> ReadPhotoAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return (null, null, "Please choose an image to upload.");

        if (file.Length > MaxBytes)
            return (null, null, "The image is too large. The maximum size is 2 MB.");

        var contentType = Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (contentType is null)
            return (null, null, "Only JPG, PNG or WEBP images are allowed.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        return (buffer.ToArray(), contentType, null);
    }
}
