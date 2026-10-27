using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbit.Core.Data;
using Orbit.Core.Providers;
using Orbit.Core.Providers.DigitalOcean;
using Orbit.Core.Security;
using Orbit.Core.Services;

namespace Orbit.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything Orbit's domain needs: SQLite persistence, encrypted-token storage,
    /// the DigitalOcean provider, the droplet/connection services and the metrics poller.
    /// </summary>
    public static IServiceCollection AddOrbitCore(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new OrbitOptions();
        configuration.GetSection(OrbitOptions.SectionName).Bind(options);
        services.Configure<OrbitOptions>(configuration.GetSection(OrbitOptions.SectionName));

        Directory.CreateDirectory(options.DataDirectory);
        Directory.CreateDirectory(options.KeyRingPath);

        // --- Persistence (SQLite, file lives under the mounted data volume) ---
        services.AddDbContext<OrbitDbContext>(o => o.UseSqlite($"Data Source={options.DatabasePath}"));

        // --- Encryption at rest for the API token (and future SSH keys) ---
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(options.KeyRingPath))
            .SetApplicationName("Orbit");
        services.AddSingleton<ITokenProtector, TokenProtector>();

        // --- DigitalOcean HTTP client + shared API plumbing ---
        services.AddHttpClient(DigitalOceanApi.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.digitalocean.com");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "Orbit/1.0 (+https://github.com)");
        });
        services.AddSingleton<DigitalOceanApi>();

        // --- Providers ---
        services.AddSingleton<IDropletProvider, DigitalOceanProvider>();
        services.AddSingleton<IProviderResolver, ProviderResolver>();
        services.AddSingleton<IDnsProvider, DigitalOceanDnsProvider>();
        services.AddSingleton<IDatabaseProvider, DigitalOceanDatabaseProvider>();

        // --- State + domain services ---
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DropletStateCache>();
        services.AddScoped<IActivityRecorder, ActivityRecorder>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IDropletService, DropletService>();
        services.AddScoped<IDomainService, DomainService>();
        services.AddScoped<IDatabaseService, DatabaseService>();
        services.AddScoped<ISshCredentialService, SshCredentialService>();
        services.AddScoped<ISshService, SshService>();
        services.AddScoped<IDropletManagementService, DropletManagementService>();

        // Real notifier is registered by the web host (SignalR); fall back to a no-op if absent.
        services.TryAddSingleton<IMetricsNotifier, NullMetricsNotifier>();

        services.AddHostedService<MetricsPoller>();

        return services;
    }

    /// <summary>Applies EF Core migrations at startup, creating the database if needed.</summary>
    public static async Task InitializeOrbitDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
