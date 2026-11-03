using Microsoft.EntityFrameworkCore;

namespace SchoolEase.Web.Infrastructure;

/// <summary>
/// Central place where the EF Core provider is selected.
///
/// Switch engines by editing <c>Database:Provider</c> in appsettings.json and
/// supplying the connection string through <c>ConnectionStrings:DefaultConnection</c>:
///   "Sqlite"    -> zero-install file database (default, great for demos)
///   "MySql"     -> point DefaultConnection at your MySQL server
///   "SqlServer" -> point DefaultConnection at your SQL Server instance
///
/// Keep real credentials out of appsettings.json: set them per environment with
/// user secrets (local) or the <c>ConnectionStrings__DefaultConnection</c>
/// environment variable (hosting).
/// </summary>
public static class DatabaseSetup
{
    public const string Sqlite = "sqlite";
    public const string MySql = "mysql";
    public const string SqlServer = "sqlserver";

    public static IServiceCollection AddSchoolEaseDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = (configuration["Database:Provider"] ?? Sqlite).Trim().ToLowerInvariant();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=schoolease.db";

        services.AddDbContext<Data.ApplicationDbContext>(options =>
        {
            switch (provider)
            {
                case MySql:
                    var version = configuration["Database:MySqlVersion"] ?? "8.0.36";
                    options.UseMySql(connectionString, ServerVersion.Parse(version),
                        my => my.EnableRetryOnFailure().MigrationsHistoryTable("__EFMigrationsHistory"));
                    break;

                case SqlServer:
                    options.UseSqlServer(connectionString,
                        sql => sql.EnableRetryOnFailure().MigrationsHistoryTable("__EFMigrationsHistory"));
                    break;

                default:
                    options.UseSqlite(connectionString,
                        sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory"));
                    break;
            }

            if (configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
            }

            options.EnableDetailedErrors();
        });

        return services;
    }

    /// <summary>Human-readable name of the active engine (shown on the Settings page).</summary>
    public static string ProviderDisplayName(IConfiguration configuration) =>
        (configuration["Database:Provider"] ?? Sqlite).Trim().ToLowerInvariant() switch
        {
            MySql => "MySQL (Pomelo.EntityFrameworkCore.MySql)",
            SqlServer => "Microsoft SQL Server",
            _ => "SQLite (file database)"
        };
}
