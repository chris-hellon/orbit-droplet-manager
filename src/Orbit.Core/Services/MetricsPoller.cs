using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>
/// Background service that polls provider metrics for every running droplet and pushes each reading
/// to connected browsers over <see cref="IMetricsNotifier"/>. Runs only while a connection exists.
/// </summary>
public sealed class MetricsPoller(
    IServiceScopeFactory scopeFactory,
    IMetricsNotifier notifier,
    IOptions<OrbitOptions> options,
    ILogger<MetricsPoller> logger) : BackgroundService
{
    private readonly OrbitOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(15, _options.MetricsPollSeconds));
        logger.LogInformation("MetricsPoller started; polling every {Seconds}s.", period.TotalSeconds);

        using var timer = new PeriodicTimer(period);
        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Metrics poll cycle failed; will retry next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var connections = scope.ServiceProvider.GetRequiredService<IConnectionService>();
        var droplets = scope.ServiceProvider.GetRequiredService<IDropletService>();

        var status = await connections.GetStatusAsync(ct);
        if (!status.Connected)
            return;

        IReadOnlyList<DropletDto> list;
        try
        {
            list = await droplets.GetDropletsAsync(ct);
        }
        catch (NotConnectedException)
        {
            return;
        }

        await notifier.DropletsRefreshedAsync(ct);

        var active = list.Where(d => d.IsPoweredOn).ToList();
        if (active.Count == 0)
            return;

        // Poll a few droplets at a time so large fleets don't burst the DO rate limit.
        using var gate = new SemaphoreSlim(4);
        var tasks = active.Select(async d =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var metrics = await droplets.RefreshMetricsAsync(d.Id, ct);
                await notifier.MetricsAsync(metrics, ct);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to poll metrics for droplet {Id}.", d.Id);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
