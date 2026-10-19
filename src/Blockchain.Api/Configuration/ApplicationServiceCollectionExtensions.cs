using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Application.Snapshots.GetSnapshot;
using Blockchain.Application.Snapshots.GetSnapshotHistory;
using Blockchain.Domain;
using FluentValidation;

namespace Blockchain.Api.Configuration;

public static class ApplicationServiceCollectionExtensions
{
    // Registration belongs to the composition root; Application does not need a DI dependency.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CaptureSnapshotCommand>, CaptureSnapshotCommandValidator>();
        services.AddScoped<IValidator<GetSnapshotQuery>, GetSnapshotQueryValidator>();
        services.AddScoped<IValidator<GetSnapshotHistoryQuery>, GetSnapshotHistoryQueryValidator>();
        services.AddScoped<ICommandHandler<CaptureSnapshotCommand, BlockchainNetworkSnapshot>, CaptureSnapshotHandler>();
        services.AddScoped<ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>>, CaptureAllSnapshotsHandler>();
        services.AddScoped<IQueryHandler<GetSnapshotQuery, BlockchainNetworkSnapshot?>, GetSnapshotHandler>();
        services.AddScoped<IQueryHandler<GetSnapshotHistoryQuery, SnapshotHistoryPage>, GetSnapshotHistoryHandler>();
        return services;
    }
}
