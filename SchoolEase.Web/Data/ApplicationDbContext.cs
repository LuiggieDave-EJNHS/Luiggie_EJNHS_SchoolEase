using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Data;

/// <summary>
/// Primary EF Core context. Extends Identity so identity tables and school tables
/// live in the same database and can participate in one transaction.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherSubjectAssignment> TeacherSubjectAssignments => Set<TeacherSubjectAssignment>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<GradeRecord> GradeRecords => Set<GradeRecord>();
    public DbSet<LearningActivitySheet> LearningActivitySheets => Set<LearningActivitySheet>();
    public DbSet<ActivitySheetTarget> ActivitySheetTargets => Set<ActivitySheetTarget>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentRequest> DocumentRequests => Set<DocumentRequest>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SchoolEvent> SchoolEvents => Set<SchoolEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ------------------------------------------------------------------
        // Identity tweaks
        // ------------------------------------------------------------------
        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.FullName).IsRequired().HasMaxLength(120);
            e.HasIndex(u => u.IsActive);
        });

        // ------------------------------------------------------------------
        // People
        // ------------------------------------------------------------------
        builder.Entity<Student>(e =>
        {
            e.HasIndex(s => s.StudentNumber).IsUnique();
            e.HasIndex(s => s.LastName);
            e.HasIndex(s => s.SectionId);

            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(s => s.Guardian)
                .WithMany(g => g.Students)
                .HasForeignKey(s => s.GuardianId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(s => s.GradeLevel)
                .WithMany(g => g.Students)
                .HasForeignKey(s => s.GradeLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Section)
                .WithMany(sec => sec.Students)
                .HasForeignKey(s => s.SectionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Teacher>(e =>
        {
            e.HasIndex(t => t.EmployeeNumber).IsUnique();
            e.HasIndex(t => t.LastName);

            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Staff>(e =>
        {
            e.HasIndex(s => s.StaffNumber).IsUnique();
            e.HasIndex(s => s.LastName);

            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Guardian>(e => e.HasIndex(g => g.LastName));

        // ------------------------------------------------------------------
        // Academic structure
        // ------------------------------------------------------------------
        builder.Entity<GradeLevel>(e =>
        {
            e.HasIndex(g => g.Code).IsUnique();
            e.HasIndex(g => g.OrderIndex);
        });

        builder.Entity<Section>(e =>
        {
            e.HasIndex(s => new { s.Code, s.SchoolYear }).IsUnique();
            e.HasIndex(s => s.GradeLevelId);

            e.HasOne(s => s.GradeLevel)
                .WithMany(g => g.Sections)
                .HasForeignKey(s => s.GradeLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Adviser)
                .WithMany(t => t.AdvisorySections)
                .HasForeignKey(s => s.AdviserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Subject>(e =>
        {
            e.HasIndex(s => s.Code).IsUnique();

            e.HasOne(s => s.GradeLevel)
                .WithMany(g => g.Subjects)
                .HasForeignKey(s => s.GradeLevelId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TeacherSubjectAssignment>(e =>
        {
            e.HasIndex(a => new { a.TeacherId, a.SubjectId, a.SectionId, a.SchoolYear }).IsUnique();

            e.HasOne(a => a.Teacher)
                .WithMany(t => t.SubjectAssignments)
                .HasForeignKey(a => a.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Subject)
                .WithMany(s => s.TeacherAssignments)
                .HasForeignKey(a => a.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Section)
                .WithMany()
                .HasForeignKey(a => a.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClassSchedule>(e =>
        {
            e.HasIndex(s => new { s.SectionId, s.DayOfWeek });
            e.HasIndex(s => new { s.TeacherId, s.DayOfWeek });
            e.HasIndex(s => new { s.Room, s.DayOfWeek });

            e.HasOne(s => s.Section)
                .WithMany(sec => sec.ClassSchedules)
                .HasForeignKey(s => s.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Subject)
                .WithMany(sub => sub.ClassSchedules)
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Teacher)
                .WithMany(t => t.ClassSchedules)
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.StudentId, x.SchoolYear });
            e.HasIndex(x => x.SectionId);

            e.HasOne(x => x.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.GradeLevel)
                .WithMany()
                .HasForeignKey(x => x.GradeLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Section)
                .WithMany(sec => sec.Enrollments)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // Records
        // ------------------------------------------------------------------
        builder.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(a => new { a.Date, a.SectionId });
            e.HasIndex(a => new { a.StudentId, a.Date });
            e.HasIndex(a => a.Status);

            e.HasOne(a => a.Student)
                .WithMany(s => s.AttendanceRecords)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Section)
                .WithMany()
                .HasForeignKey(a => a.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Subject)
                .WithMany()
                .HasForeignKey(a => a.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(a => a.RecordedBy)
                .WithMany()
                .HasForeignKey(a => a.RecordedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<GradeRecord>(e =>
        {
            e.HasIndex(g => new { g.StudentId, g.SchoolYear, g.Term });
            e.HasIndex(g => new { g.SectionId, g.SubjectId, g.Term });
            e.HasIndex(g => new { g.StudentId, g.SubjectId, g.SchoolYear, g.Term }).IsUnique();

            e.HasOne(g => g.Student)
                .WithMany(s => s.GradeRecords)
                .HasForeignKey(g => g.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.Subject)
                .WithMany()
                .HasForeignKey(g => g.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(g => g.Section)
                .WithMany()
                .HasForeignKey(g => g.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(g => g.Teacher)
                .WithMany()
                .HasForeignKey(g => g.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(g => g.EncodedBy)
                .WithMany()
                .HasForeignKey(g => g.EncodedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // Learning resources
        // ------------------------------------------------------------------
        builder.Entity<LearningActivitySheet>(e =>
        {
            e.HasIndex(x => x.SubjectId);
            e.HasIndex(x => x.GradeLevelId);
            e.HasIndex(x => x.IsPublished);

            e.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.GradeLevel)
                .WithMany()
                .HasForeignKey(x => x.GradeLevelId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ActivitySheetTarget>(e =>
        {
            e.HasIndex(t => new { t.ActivitySheetId, t.SectionId }).IsUnique();
            e.HasIndex(t => t.SectionId);

            e.HasOne(t => t.ActivitySheet)
                .WithMany(x => x.Targets)
                .HasForeignKey(t => t.ActivitySheetId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.Section)
                .WithMany()
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------------
        // Document requests
        // ------------------------------------------------------------------
        builder.Entity<DocumentType>(e => e.HasIndex(d => d.Code).IsUnique());

        builder.Entity<DocumentRequest>(e =>
        {
            e.HasIndex(r => r.RequestNumber).IsUnique();
            e.HasIndex(r => r.Status);
            e.HasIndex(r => new { r.StudentId, r.Status });

            e.HasOne(r => r.Student)
                .WithMany(s => s.DocumentRequests)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.DocumentType)
                .WithMany(d => d.Requests)
                .HasForeignKey(r => r.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.ProcessedBy)
                .WithMany()
                .HasForeignKey(r => r.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // Communications
        // ------------------------------------------------------------------
        builder.Entity<Announcement>(e =>
        {
            e.HasIndex(a => new { a.IsPublished, a.PublishDate });
            e.HasIndex(a => a.Audience);

            e.HasOne(a => a.Author)
                .WithMany()
                .HasForeignKey(a => a.AuthorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.IsRead });
            e.HasIndex(n => n.CreatedAt);

            e.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SchoolEvent>(e =>
        {
            e.HasIndex(x => x.StartDate);
            e.HasIndex(x => x.Category);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // System
        // ------------------------------------------------------------------
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => new { a.EntityName, a.EntityId });
            e.HasIndex(a => a.UserId);
        });

        builder.Entity<SystemSetting>(e => e.HasIndex(s => s.Key).IsUnique());
    }
}
