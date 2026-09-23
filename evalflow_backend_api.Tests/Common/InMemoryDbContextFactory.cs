using evalflow_backend_api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Creates isolated, in-memory AppDbContext instances for handler unit tests.
/// Each call gets its own database, so tests never interfere with each other.
/// </summary>
public static class InMemoryDbContextFactory
{
    /// <summary>
    /// Creates a new AppDbContext backed by an in-memory database.
    ///
    /// Pass the same <paramref name="root"/> as another context created with the same
    /// <paramref name="name"/> to have them share data (EF Core's in-memory provider only
    /// shares a store across separately-built DbContextOptions when they're pointed at the
    /// same InMemoryDatabaseRoot) — see CustomWebApplicationFactory for why this matters.
    /// </summary>
    public static AppDbContext Create(string? name = null, InMemoryDatabaseRoot? root = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString(), root)
            .EnableSensitiveDataLogging()
            .Options;

        return new AppDbContext(options);
    }
}
