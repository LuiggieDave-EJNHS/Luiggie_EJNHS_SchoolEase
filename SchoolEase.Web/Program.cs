using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Infrastructure;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Mail"));

// ---------------------------------------------------------------------------
// Database (SQLite by default, MySQL / SQL Server selectable from appsettings)
// ---------------------------------------------------------------------------
builder.Services.AddSchoolEaseDatabase(builder.Configuration);

// ---------------------------------------------------------------------------
// Authentication - ASP.NET Core Identity with hashed passwords + lockout
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<SchoolEaseClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "SchoolEase.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ---------------------------------------------------------------------------
// Data protection - persist the key ring to disk so sign-in cookies and
// anti-forgery tokens survive application restarts and IIS app-pool recycles.
// Without this the keys live in memory only and every restart logs everyone out.
// ---------------------------------------------------------------------------
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keysPath))
{
    keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
}

Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("SchoolEase");

// ---------------------------------------------------------------------------
// Role-based authorization policies
// ---------------------------------------------------------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Administrators));
    options.AddPolicy(Policies.SuperAdminOnly, p => p.RequireRole(Roles.SuperAdministrator));
    options.AddPolicy(Policies.StaffOnly, p => p.RequireRole(Roles.NonStudent));
    options.AddPolicy(Policies.TeacherOrAdmin, p => p.RequireRole(Roles.Teacher, Roles.SuperAdministrator, Roles.SchoolAdministrator, Roles.IctHeadCoordinator));
    options.AddPolicy(Policies.AcademicOversight, p => p.RequireRole(Roles.AcademicOversight));
    options.AddPolicy(Policies.CanPostAnnouncements, p => p.RequireRole(Roles.Administrators));
    options.AddPolicy(Policies.CanManageRequests, p => p.RequireRole(Roles.Administrators));
});

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddControllersWithViews(options =>
{
    // Anti-forgery protection on every unsafe verb by default.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRazorPages();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Reverse-proxy support (Render, Azure, nginx, any container host).
// Without this the app only sees plain HTTP from the proxy, so redirects and
// the links inside confirmation e-mails would be generated as http://.
// ---------------------------------------------------------------------------
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// ---------------------------------------------------------------------------
// Database migration + seeding
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Data/Migrations holds SQLite-flavoured migrations, so SQLite applies them
        // as usual. Other engines (SQL Server / MySQL) build the schema straight from
        // the current model instead, which keeps hosted deployments working.
        if (config["Database:Provider"]?.Trim().Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (config.GetValue("Seed:Enabled", true))
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }

        logger.LogInformation("SchoolEase database is ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialisation failed.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

// Free hosting tiers usually serve plain HTTP until HTTPS is provisioned, so the
// redirect is opt-out through Https:Redirect. Locally it stays on.
if (app.Configuration.GetValue("Https:Redirect", true))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
