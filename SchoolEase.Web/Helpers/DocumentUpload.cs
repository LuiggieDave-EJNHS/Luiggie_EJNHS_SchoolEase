using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Services;

namespace SchoolEase.Web.Helpers;

/// <summary>
/// Shared rules for putting a scanned record into a learner's 201 file. Both the staff
/// upload page and the learner self-upload page go through here, so the size cap, the
/// allowed file types and the storage fallback can never drift apart.
/// </summary>
public static class DocumentUpload
{
    public const long MaxFileSize = 20 * 1024 * 1024; // 20 MB

    /// <summary>Accept list for a document picker: scans, photos and Office files.</summary>
    public const string AcceptAttribute = ".pdf,.jpg,.jpeg,.png,.doc,.docx,.xls,.xlsx,.ppt,.pptx";

    public static readonly string[] AllowedExtensions =
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"
    };

    public static void Validate(IFormFile? file, ModelStateDictionary modelState, string key, bool required)
    {
        if (file is null)
        {
            if (required) modelState.AddModelError(key, "Please choose a file to upload.");
            return;
        }

        if (file.Length == 0)
        {
            modelState.AddModelError(key, "The selected file is empty.");
            return;
        }

        if (file.Length > MaxFileSize)
        {
            modelState.AddModelError(key,
                $"The file is too large. The maximum size is {LearningActivitySheet.FileSizeLabelOf(MaxFileSize)}.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            modelState.AddModelError(key, "Allowed file types: PDF, JPG, PNG, Word, Excel and PowerPoint.");
        }
    }

    /// <summary>
    /// Puts the upload in object storage when a bucket is configured, otherwise in the
    /// row itself. The content type is derived from the extension rather than trusted
    /// from the browser so a crafted upload cannot mislabel itself.
    /// </summary>
    public static async Task StoreAsync(StudentDocument document, IFormFile file, IFileStorage storage)
    {
        document.FileName = Path.GetFileName(file.FileName);
        document.ContentType = ContentTypeFor(Path.GetExtension(document.FileName));
        document.FileSize = file.Length;
        document.StorageKey = null;
        document.FileContent = Array.Empty<byte>();

        if (storage.IsConfigured)
        {
            await using var stream = file.OpenReadStream();
            document.StorageKey = await storage.UploadAsync(document.FileName, stream, document.ContentType);
            return;
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        document.FileContent = buffer.ToArray();
    }

    public static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}
