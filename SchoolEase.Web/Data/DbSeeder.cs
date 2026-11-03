using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;

namespace SchoolEase.Web.Data;

/// <summary>Sample accounts created only when demo data seeding is enabled, re-used by the demo data seeder.</summary>
public sealed class SeededAccounts
{
    public AppUser SuperAdmin { get; set; } = null!;
    public AppUser Admin { get; set; } = null!;
    public AppUser Coordinator { get; set; } = null!;
    public AppUser Teacher { get; set; } = null!;
    public AppUser Teacher2 { get; set; } = null!;
    public AppUser Staff { get; set; } = null!;
    public AppUser Registrar { get; set; } = null!;
    public AppUser Student { get; set; } = null!;
    public AppUser Student2 { get; set; } = null!;
}

/// <summary>
/// Creates the role set, the system settings and the first administrator account.
/// No demo accounts are created: the only account seeded by default is the
/// administrator supplied through Seed:SuperAdminEmail / Seed:SuperAdminPassword.
/// A realistic Philippine senior-high dataset can still be requested explicitly
/// with Seed:DemoData=true, which also brings its own sample accounts.
/// Everything here is idempotent and safe to run on every application start.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SchoolEase.Seed");

        await SeedRolesAsync(roleManager, userManager);

        await SeedSettingsAsync(db);

        await SeedDocumentTypesAsync(db);

        await SeedSchoolDirectoryAsync(db);

        // The first administrator is created from configuration, so a fresh
        // install always has a way in without shipping any known credentials.
        await SeedAdministratorAsync(userManager, config, logger);

        // Sample content is for local demos only and stays off unless
        // Seed:DemoData is explicitly switched on. A live school site never needs it.
        if (!config.GetValue("Seed:DemoData", false)) return;
        if (await db.Students.AnyAsync()) return;

        var accounts = await SeedAccountsAsync(userManager);
        await DemoDataSeeder.SeedAsync(db, accounts);
    }

    /// <summary>
    /// Creates the first administrator from Seed:SuperAdminEmail / Seed:SuperAdminPassword
    /// (env vars Seed__SuperAdminEmail / Seed__SuperAdminPassword on a host). Runs in every
    /// environment and is idempotent, so it can safely stay configured after the first run.
    /// </summary>
    private static async Task SeedAdministratorAsync(
        UserManager<AppUser> userManager, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:SuperAdminEmail"];
        var password = config["Seed:SuperAdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!await userManager.Users.AnyAsync())
            {
                logger.LogWarning(
                    "No administrator account exists yet. Set Seed:SuperAdminEmail and Seed:SuperAdminPassword " +
                    "(environment variables Seed__SuperAdminEmail / Seed__SuperAdminPassword on a host) " +
                    "so the first administrator is created on startup.");
            }

            return;
        }

        await EnsureUserAsync(userManager, email.Trim(), password,
            config["Seed:SuperAdminName"] ?? "System Administrator", Roles.SuperAdministrator);
        logger.LogInformation("Administrator account {Email} is ready.", email);
    }

    // ==================================================================
    // Roles
    // ==================================================================
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, UserManager<AppUser> userManager)
    {
        // Older databases named the registrar role "Staff". Rename it in place so
        // accounts that already hold it keep working.
        const string legacyRole = "Staff";
        var legacy = await roleManager.FindByNameAsync(legacyRole);
        if (legacy is not null)
        {
            if (!await roleManager.RoleExistsAsync(Roles.Registrar))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Registrar));
            }

            foreach (var member in await userManager.GetUsersInRoleAsync(legacyRole))
            {
                await userManager.RemoveFromRoleAsync(member, legacyRole);
                await userManager.AddToRoleAsync(member, Roles.Registrar);
            }

            await roleManager.DeleteAsync(legacy);
        }

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    // ==================================================================
    // Accounts
    // ==================================================================
    private static async Task<SeededAccounts> SeedAccountsAsync(UserManager<AppUser> userManager) =>
        new()
        {
            SuperAdmin = await EnsureUserAsync(userManager, "superadmin@schoolease.ph", "SuperAdmin@2025", "Engr. Ricardo Villamor", Roles.SuperAdministrator),
            Admin = await EnsureUserAsync(userManager, "admin@schoolease.ph", "Admin@2025", "Dr. Socorro Manalo", Roles.SchoolAdministrator),
            Coordinator = await EnsureUserAsync(userManager, "ictcoordinator@schoolease.ph", "Coordinator@2025", "Engr. Ferdinand Cruz", Roles.IctHeadCoordinator),
            Teacher = await EnsureUserAsync(userManager, "teacher@schoolease.ph", "Teacher@2025", "Maria Elena Santos", Roles.Teacher),
            Teacher2 = await EnsureUserAsync(userManager, "teacher2@schoolease.ph", "Teacher@2025", "Michael Aquino", Roles.Teacher),
            Staff = await EnsureUserAsync(userManager, "staff@schoolease.ph", "Staff@2025", "Rosario Villanueva", Roles.Registrar),
            Registrar = await EnsureUserAsync(userManager, "registrar@schoolease.ph", "Staff@2025", "Domingo Pascual", Roles.Registrar),
            Student = await EnsureUserAsync(userManager, "student@schoolease.ph", "Student@2025", "Juan Miguel Dela Rosa", Roles.Student),
            Student2 = await EnsureUserAsync(userManager, "student2@schoolease.ph", "Student@2025", "Ana Marie Bautista", Roles.Student)
        };

    private static async Task<AppUser> EnsureUserAsync(
        UserManager<AppUser> userManager, string email, string password, string fullName, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                SecurityStamp = Guid.NewGuid().ToString(),
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo user {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    // ==================================================================
    // System settings
    // ==================================================================
    private static async Task SeedSettingsAsync(ApplicationDbContext db)
    {
        if (await db.SystemSettings.AnyAsync()) return;

        db.SystemSettings.AddRange(
            new SystemSetting { Key = "School.Name", Value = SchoolInfo.SchoolName, Description = "Printed on all reports and documents.", Group = "School Profile" },
            new SystemSetting { Key = "School.Address", Value = SchoolInfo.SchoolAddress, Description = "School address shown on report headers.", Group = "School Profile" },
            new SystemSetting { Key = "School.Division", Value = SchoolInfo.Division, Description = "Schools division office.", Group = "School Profile" },
            new SystemSetting { Key = "School.Region", Value = SchoolInfo.Region, Description = "Administrative region.", Group = "School Profile" },
            new SystemSetting { Key = "School.Principal", Value = "Dr. Socorro Manalo", Description = "Name signed on printed documents.", Group = "School Profile" },
            new SystemSetting { Key = "Academic.CurrentSchoolYear", Value = SchoolInfo.SchoolYear, Description = "Active school year for new records.", Group = "Academic" },
            new SystemSetting { Key = "Academic.ActiveTerm", Value = "1", Description = "Default term for encoding.", Group = "Academic" },
            new SystemSetting { Key = "Academic.PassingGrade", Value = "75", Description = "Minimum passing grade for a subject.", Group = "Academic" },
            new SystemSetting { Key = "Requests.AutoNumberPrefix", Value = "REQ", Description = "Prefix for generated request numbers.", Group = "Document Requests" },
            new SystemSetting { Key = "Requests.DefaultProcessingDays", Value = "3", Description = "Fallback turnaround time in days.", Group = "Document Requests" },
            new SystemSetting { Key = "Notifications.EmailEnabled", Value = "false", Description = "Reserved for future e-mail notifications.", Group = "Notifications" },
            new SystemSetting { Key = "Security.SessionTimeoutMinutes", Value = "480", Description = "Session timeout in minutes.", Group = "Security" }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedDocumentTypesAsync(ApplicationDbContext db)
    {
        if (await db.DocumentTypes.AnyAsync()) return;

        db.DocumentTypes.AddRange(
            new DocumentType { Code = "SF9", Name = "SF9 (Report Card)", Description = "Progress Report Card for the current school year.", ProcessingDays = 3 },
            new DocumentType { Code = "SF10", Name = "SF10 (Permanent Record)", Description = "Official Form 137 / Learner Permanent Record.", ProcessingDays = 7 },
            new DocumentType { Code = "GMC", Name = "Certificate of Good Moral Character", Description = "Certification of good conduct for transfer or employment.", ProcessingDays = 2 },
            new DocumentType { Code = "COE", Name = "Certificate of Enrollment", Description = "Certification that the student is currently enrolled.", ProcessingDays = 1 },
            new DocumentType { Code = "COG", Name = "Certificate of Graduation", Description = "Certification of completion of senior high school requirements.", ProcessingDays = 2 },
            new DocumentType { Code = "DIPL", Name = "Diploma (Certified True Copy)", Description = "Certified copy of the original high school diploma.", ProcessingDays = 3 },
            new DocumentType { Code = "HD", Name = "Honorable Dismissal", Description = "Transfer credential for students leaving the institution.", ProcessingDays = 5 }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedSchoolDirectoryAsync(ApplicationDbContext db)
    {
        if (await db.SchoolContacts.AnyAsync()) return;

        db.SchoolContacts.AddRange(
            new SchoolContact { Name = "Manila Science High School", SchoolId = "305309", Division = "Manila", Region = "NCR", Email = "manilascience@deped.gov.ph", ContactNumber = "(02) 8525-6197", Address = "Taft Ave, Ermita, Manila" },
            new SchoolContact { Name = "Ramon Magsaysay High School", SchoolId = "305315", Division = "Manila", Region = "NCR", Email = "ramonmagsaysayhs@deped.gov.ph", ContactNumber = "(02) 8735-1234", Address = "España Blvd, Sampaloc, Manila" },
            new SchoolContact { Name = "Araullo High School", SchoolId = "305301", Division = "Manila", Region = "NCR", Email = "araullohs@deped.gov.ph", ContactNumber = "(02) 8527-2101", Address = "United Nations Ave, Ermita, Manila" },
            new SchoolContact { Name = "Quezon City High School", SchoolId = "305389", Division = "Quezon City", Region = "NCR", Email = "qchs@deped.gov.ph", ContactNumber = "(02) 8928-1234", Address = "Scout Ybardolaza, Kamuning, QC" },
            new SchoolContact { Name = "Pasig City Science High School", SchoolId = "305412", Division = "Pasig City", Region = "NCR", Email = "pasigscience@deped.gov.ph", ContactNumber = "(02) 8641-1234", Address = "Rainforest Park, Maybunga, Pasig" }
        );

        await db.SaveChangesAsync();
    }
}
