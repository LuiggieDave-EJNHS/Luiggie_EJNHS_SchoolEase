using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SchoolEase.Web.Infrastructure;

/// <summary>
/// Central place where the EF Core provider is selected.
///
/// Switch engines by editing <c>Database:Provider</c> in appsettings.json and
/// supplying the connection string through <c>ConnectionStrings:DefaultConnection</c>:
///   "Sqlite"    -> zero-install file database (default, great for demos)
///   "MySql"     -> point DefaultConnection at your MySQL server
///   "Postgres"  -> point DefaultConnection at your PostgreSQL server
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
    public const string Postgres = "postgres";
    public const string SqlServer = "sqlserver";

    public static IServiceCollection AddSchoolEaseDatabase(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        var provider = (configuration["Database:Provider"] ?? Sqlite).Trim().ToLowerInvariant();
        if (provider is "postgresql" or "pgsql") provider = Postgres;

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

                case Postgres:
                    options.UseNpgsql(CapPostgresPool(connectionString),
                        npgsql => npgsql.EnableRetryOnFailure().MigrationsHistoryTable("__EFMigrationsHistory"));
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

            // Detailed errors expose query parameters in logs and add overhead on
            // every query, so they stay out of production.
            if (isDevelopment)
            {
                options.EnableDetailedErrors();
            }
        });

        return services;
    }

    /// <summary>
    /// Keeps the connection pool inside what a free PostgreSQL tier accepts.
    /// Free tiers reject connections past their limit, which turns a traffic spike
    /// into hard errors; queueing inside the pool is the gentler failure. Left as
    /// configured when the connection string already sets a size.
    /// </summary>
    private static string CapPostgresPool(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!builder.ContainsKey("Maximum Pool Size"))
            {
                builder.MaxPoolSize = 20;
            }

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            // Not a key/value Npgsql string (or malformed) - pass it through untouched.
            return connectionString;
        }
    }

    /// <summary>Human-readable name of the active engine (shown on the Settings page).</summary>
    public static string ProviderDisplayName(IConfiguration configuration)
    {
        var provider = (configuration["Database:Provider"] ?? Sqlite).Trim().ToLowerInvariant();
        if (provider is "postgresql" or "pgsql") provider = Postgres;

        return provider switch
        {
            MySql => "MySQL (Pomelo.EntityFrameworkCore.MySql)",
            Postgres => "PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL)",
            SqlServer => "Microsoft SQL Server",
            _ => "SQLite (file database)"
        };
    }
}
