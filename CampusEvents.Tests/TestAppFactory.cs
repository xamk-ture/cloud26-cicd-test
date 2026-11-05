using CampusEvents.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusEvents.Tests;

/// <summary>
/// Boots the real app (Program.cs) but replaces PostgreSQL with an
/// in-memory EF Core provider, so tests run anywhere — including CI.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    // The admin API key the tests present — any value works, the real one lives in Key Vault.
    public const string AdminApiKey = "test-key";

    // Unique name per factory instance → tests do not share state.
    private readonly string _databaseName = $"CampusEventsTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configuration the lock reads — without it the filter fails closed (503) and every admin test fails.
        builder.UseSetting("AdminApiKey", AdminApiKey);

        builder.ConfigureServices(services =>
        {
            // Remove the real PostgreSQL registration from Program.cs...
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            // ...and register the in-memory provider instead.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
