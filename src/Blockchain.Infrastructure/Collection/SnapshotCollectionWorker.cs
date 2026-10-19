using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blockchain.Infrastructure.Collection;

public sealed class SnapshotCollectionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SnapshotCollectionOptions> options,
    TimeProvider clock,
    ILogger<SnapshotCollectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Background snapshot collection is disabled");
            return;
        }

        logger.LogInformation("Background snapshot collection enabled; delay between cycles {IntervalSeconds}s",
            settings.IntervalSeconds);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.InitialDelaySeconds), clock, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await CaptureCycleAsync(settings, stoppingToken);
                // Fixed delay after completion: slow cycles cannot overlap or accumulate missed ticks.
                await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Background snapshot collection stopped");
        }
    }

    private async Task CaptureCycleAsync(SnapshotCollectionOptions settings, CancellationToken stoppingToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(settings.CycleTimeoutSeconds), clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<
                ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>>>();
            var results = await handler.HandleAsync(new(), cancellation.Token);
            foreach (var result in results.Where(x => !x.Succeeded))
                logger.LogWarning("Background capture failed for {Coin}/{Network}: {Error}",
                    result.Coin, result.Network, result.Error);
            logger.LogInformation("Background capture cycle completed: {Succeeded}/{Total} snapshots saved",
                results.Count(x => x.Succeeded), results.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            logger.LogWarning("Background capture cycle exceeded {TimeoutSeconds}s; committed snapshots are retained",
                settings.CycleTimeoutSeconds);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Background capture cycle failed; committed snapshots are retained; next cycle follows the configured delay");
        }
    }
}
