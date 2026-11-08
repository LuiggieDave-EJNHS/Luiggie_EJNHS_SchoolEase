using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using SchoolEase.Web.Data;
using SchoolEase.Web.Infrastructure;
using SchoolEase.Web.Models;
using SchoolEase.Web.Models.Entities;
using SchoolEase.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Security: Hide the Server header from Kestrel
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Fix for Npgsql 6.0+ DateTime issue when using DateTime.Today/Now with timestamp with time zone
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));

// ---------------------------------------------------------------------------
// Database (SQLite by default, MySQL / SQL Server selectable from appsettings)
// ---------------------------------------------------------------------------
builder.Services.AddSchoolEaseDatabase(builder.Configuration, builder.Environment.IsDevelopment());

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
        options.Password.RequireNonAlphanumeric = true;

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
// Response compression. Bootstrap, the site stylesheet and every rendered page
// leave the server uncompressed otherwise, which is the single biggest cost on a
// small host. Fastest (not Optimal) because CPU is the scarce resource here.
// ---------------------------------------------------------------------------
builder.Services.AddResponseCompression(options =>
{
    // TLS is usually terminated by the host's proxy, but keep this on so
    // direct HTTPS requests are compressed too.
    options.EnableForHttps = true;

    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/javascript",
        "text/javascript",
        "application/json",
        "image/svg+xml"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// In-memory cache for the role dashboards (see DashboardService).
builder.Services.AddMemoryCache();

// ---------------------------------------------------------------------------
// Application services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Optional S3-compatible object storage (Cloudflare R2). When the credentials are
// blank, IsConfigured is false and uploaded files are kept in the database instead.
builder.Services.AddSingleton<IFileStorage, R2FileStorage>();

builder.Services.AddControllersWithViews(options =>
{
    // Anti-forgery protection on every unsafe verb by default.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRazorPages();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

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
// Security Headers (Improves grade on securityheaders.com)
// ---------------------------------------------------------------------------
app.Use(async (context, next) =>
{
    // Prevent MIME-type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    
    // Prevent the site from being embedded in an iframe (Clickjacking)
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    
    // Control how much info is sent in the Referer header
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Disable unused browser features
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), interest-cohort=()");
    
    // Content Security Policy (CSP)
    // - default-src 'self': Only allow content from our own origin by default
    // - script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net: Allow our scripts, inline chart data, and Chart.js CDN
    // - style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net: Allow our styles and Bootstrap Icons CDN
    // - img-src 'self' data: https:; Allow images from self, data URIs, and external HTTPS (Trae API)
    // - font-src 'self' data: https://cdn.jsdelivr.net; Allow fonts from self and Bootstrap Icons CDN
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data: https://cdn.jsdelivr.net; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "upgrade-insecure-requests;");
        
    await next();
});

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
    // Strictly enforce HTTPS with a 1-year max age, including subdomains and preload.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

// Free hosting tiers usually serve plain HTTP until HTTPS is provisioned, so the
// redirect is opt-out through Https:Redirect. Locally it stays on.
if (app.Configuration.GetValue("Https:Redirect", true))
{
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

// Long-lived cache headers for library assets so browsers stop re-downloading
// Bootstrap and jQuery on every page view. Images get a shorter window so a
// replaced logo still reaches users within a day. Pages themselves stay uncached.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? string.Empty;
        var maxAge = path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ? 604800 : 86400;
        ctx.Context.Response.Headers.CacheControl = $"public,max-age={maxAge}";
    }
});
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
