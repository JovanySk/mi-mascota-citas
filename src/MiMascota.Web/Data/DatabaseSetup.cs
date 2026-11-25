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
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSetup));

        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync();
            await AddMissingSqliteSchemaAsync(db);
        }
        else
        {
            await db.Database.MigrateAsync();
        }

        // Roles, demo staff (Development only) and the first admin from configuration.
        await StaffSeeder.SeedAsync(services, app.Environment, app.Configuration, logger);

        if (app.Configuration.GetValue("Database:SeedDemoData", true))
        {
            var clinic = services.GetRequiredService<IOptions<ClinicOptions>>().Value;
            var time = services.GetRequiredService<TimeProvider>();
            await SeedData.InitializeAsync(db, clinic, time);
        }
    }

    /// <summary>
    /// EnsureCreated never changes an existing SQLite file, so a local mimascota.db created
    /// by an older version gets the newer columns, tables and indexes here (SQL Server uses migrations).
    /// </summary>
    private static async Task AddMissingSqliteSchemaAsync(AppDbContext db)
    {
        var hasPhotoColumn = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('Pets') WHERE name = 'PhotoPath'")
            .SingleAsync() > 0;

        if (!hasPhotoColumn)
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Pets\" ADD COLUMN \"PhotoPath\" TEXT NULL");
        }

        // Every table and index of the current model that the file is missing (e.g. ContactMessages
        // or the Identity tables AspNetUsers, AspNetRoles...), with the same SQL EnsureCreated uses.
        foreach (var statement in CreateIfMissingStatements(db.Database.GenerateCreateScript()))
        {
            await db.Database.ExecuteSqlRawAsync(statement);
        }

        // Pet photos used to be public files under /uploads/mascotas; now they are served to staff only.
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Pets\" SET \"PhotoPath\" = '/fotos/mascotas/' || substr(\"PhotoPath\", 19) WHERE \"PhotoPath\" LIKE '/uploads/mascotas/%'");
    }

    /// <summary>The CREATE TABLE / CREATE INDEX statements of a SQLite create script, made idempotent.</summary>
    public static IEnumerable<string> CreateIfMissingStatements(string createScript)
    {
        var statements = System.Text.RegularExpressions.Regex.Split(createScript, @";\s*(?:\r?\n|$)");
        foreach (var raw in statements)
        {
            var statement = raw.Trim();
            if (statement.StartsWith("CREATE TABLE ", StringComparison.Ordinal))
            {
                yield return "CREATE TABLE IF NOT EXISTS " + statement["CREATE TABLE ".Length..];
            }
            else if (statement.StartsWith("CREATE UNIQUE INDEX ", StringComparison.Ordinal))
            {
                yield return "CREATE UNIQUE INDEX IF NOT EXISTS " + statement["CREATE UNIQUE INDEX ".Length..];
            }
            else if (statement.StartsWith("CREATE INDEX ", StringComparison.Ordinal))
            {
                yield return "CREATE INDEX IF NOT EXISTS " + statement["CREATE INDEX ".Length..];
            }
        }
    }

    private static bool UsesSqlite(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
}
