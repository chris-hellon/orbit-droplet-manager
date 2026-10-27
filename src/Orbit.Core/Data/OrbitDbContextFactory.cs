using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orbit.Core.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> can build the context without the full host.
/// Runtime configuration lives in <c>ServiceCollectionExtensions.AddOrbitCore</c>.
/// </summary>
public sealed class OrbitDbContextFactory : IDesignTimeDbContextFactory<OrbitDbContext>
{
    public OrbitDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrbitDbContext>()
            .UseSqlite("Data Source=orbit-design.db")
            .Options;
        return new OrbitDbContext(options);
    }
}
