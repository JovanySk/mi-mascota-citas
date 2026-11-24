using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MiMascota.Web.Data;

/// <summary>
/// Used only by the "dotnet ef" tool so migrations are always generated for SQL Server,
/// even though local development runs on SQLite. It never connects to a database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=MiMascota;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new AppDbContext(options);
    }
}
