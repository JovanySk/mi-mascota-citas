using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Options;

namespace MiMascota.Web.Data;

/// <summary>
/// Chooses the database provider from configuration and prepares the database at startup.
///
/// - Database:Provider = "Sqlite"    → local file database, created with EnsureCreated (zero setup).
/// - Database:Provider = "SqlServer" → connection string ConnectionStrings:SqlServer, schema applied with EF migrations.
/// </summary>
public static class DatabaseSetup
{
    public static IServiceCollection AddClinicDatabase(this IServiceCollection services)
    {
        // The configuration is read when the DbContext is created (not at startup),
        // so tests can override it easily.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            if (UsesSqlite(configuration))
            {
                var sqlite = configuration.GetConnectionString("Sqlite") ?? "Data Source=mimascota.db";
                options.UseSqlite(sqlite);
            }
            else
            {
                var sqlServer = configuration.GetConnectionString("SqlServer")
                    ?? throw new InvalidOperationException(
                        "Missing connection string 'ConnectionStrings:SqlServer'. " +
                        "Set it with the environment variable ConnectionStrings__SqlServer.");
                options.UseSqlServer(sqlServer);
            }
        });

        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();

        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync();
            await AddMissingSqliteColumnsAsync(db);
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        if (app.Configuration.GetValue("Database:SeedDemoData", true))
        {
            var clinic = services.GetRequiredService<IOptions<ClinicOptions>>().Value;
            var time = services.GetRequiredService<TimeProvider>();
            await SeedData.InitializeAsync(db, clinic, time);
        }
    }

    /// <summary>
    /// EnsureCreated never changes an existing SQLite file, so a local mimascota.db created
    /// before the pet photos existed gets the new column here (SQL Server uses migrations).
    /// </summary>
    private static async Task AddMissingSqliteColumnsAsync(AppDbContext db)
    {
        var hasPhotoColumn = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('Pets') WHERE name = 'PhotoPath'")
            .SingleAsync() > 0;

        if (!hasPhotoColumn)
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Pets\" ADD COLUMN \"PhotoPath\" TEXT NULL");
        }
    }

    private static bool UsesSqlite(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
}
