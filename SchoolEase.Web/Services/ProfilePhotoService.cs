using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Services;

/// <summary>Reads and writes the profile photos stored against user accounts.</summary>
public interface IProfilePhotoService
{
    Task<bool> HasPhotoAsync(string userId);
    Task<UserPhoto?> GetAsync(string userId);
    Task SaveAsync(string userId, byte[] content, string contentType);
    Task RemoveAsync(string userId);
}

public class ProfilePhotoService : IProfilePhotoService
{
    private readonly ApplicationDbContext _db;

    public ProfilePhotoService(ApplicationDbContext db) => _db = db;

    public Task<bool> HasPhotoAsync(string userId) =>
        _db.UserPhotos.AnyAsync(p => p.UserId == userId);

    public Task<UserPhoto?> GetAsync(string userId) =>
        _db.UserPhotos.FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task SaveAsync(string userId, byte[] content, string contentType)
    {
        var existing = await _db.UserPhotos.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existing is null)
        {
            _db.UserPhotos.Add(new UserPhoto
            {
                UserId = userId,
                Content = content,
                ContentType = contentType,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Content = content;
            existing.ContentType = contentType;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(string userId)
    {
        var existing = await _db.UserPhotos.FirstOrDefaultAsync(p => p.UserId == userId);
        if (existing is null) return;

        _db.UserPhotos.Remove(existing);
        await _db.SaveChangesAsync();
    }
}
