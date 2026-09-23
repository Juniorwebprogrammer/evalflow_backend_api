using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace evalflow_backend_api.Tests.Common;

/// <summary>
/// Spins up the real API pipeline (routing, endpoint filters, auth, MediatR) against an
/// isolated in-memory database and a fake email sender, so integration tests exercise the
/// actual HTTP endpoints without needing Postgres, SMTP, or Docker.
///
/// A brand new in-memory database is created per factory instance (see DbName), so create a
/// fresh CustomWebApplicationFactory per test (or per test class) to keep tests isolated.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DbName { get; } = $"TestDb-{Guid.NewGuid()}";
    public FakeEmailService FakeEmailService { get; } = new();

    // Shared with CreateDbContext() below: EF Core's in-memory provider only shares a store
    // between separately-built DbContextOptions when they're pointed at the same root.
    private readonly InMemoryDatabaseRoot _dbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Secrets no longer live in appsettings.json (they come from env vars / user-secrets),
        // so give the test host its own throwaway values.
        builder.UseSetting("ApiKey", IntegrationTestBase.ValidApiKey);
        builder.UseSetting("Jwt:Secret", "test-only-jwt-secret-at-least-32-bytes-long!!");
        builder.UseSetting("Encryption:Key", "test-only-encryption-key-32bytes");

        builder.ConfigureServices(services =>
        {
            // Swap the real Postgres-backed AppDbContext for an isolated in-memory one.
            // AddDbContext composes configuration across calls (it doesn't just replace the
            // previous registration), so simply removing DbContextOptions<AppDbContext> and
            // re-adding it still leaves Npgsql's configuration in the mix ("only a single
            // database provider can be registered"). Strip every service touching
            // AppDbContext first, then register it fresh with just the in-memory provider.
            var dbDescriptors = services
                .Where(d => (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext)))
                    || d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in dbDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(DbName, _dbRoot));

            // Never send real emails during tests.
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(FakeEmailService);

            // The reminder job polls the database on a 24h loop; keep it out of test runs.
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>
    /// Hands back a fresh AppDbContext pointed at the same named in-memory database the
    /// running app uses, so tests can seed/read data around a request. EF Core's in-memory
    /// provider shares a store process-wide by database name, so this stays in sync with
    /// whatever the app's own scoped AppDbContext instances see.
    /// </summary>
    public AppDbContext CreateDbContext() => InMemoryDbContextFactory.Create(DbName, _dbRoot);
}
