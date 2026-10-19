using System.ComponentModel.DataAnnotations;
using Blockchain.Api.Contracts;
using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureSnapshot;
using Blockchain.Application.Snapshots.GetSnapshot;
using Blockchain.Application.Snapshots.GetSnapshotHistory;
using Blockchain.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api/chains/{coin}/{network}/snapshots")]
public sealed class SnapshotsController(
    ICommandHandler<CaptureSnapshotCommand, BlockchainNetworkSnapshot> capture,
    IQueryHandler<GetSnapshotQuery, BlockchainNetworkSnapshot?> getSnapshot,
    IQueryHandler<GetSnapshotHistoryQuery, SnapshotHistoryPage> getHistory) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("capture")]
    [ProducesResponseType<SnapshotResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<SnapshotResponse>> CaptureSnapshot(string coin, string network, CancellationToken token)
    {
        var chain = ResolveNetwork(coin, network);
        var snapshot = await capture.HandleAsync(new(chain), token);
        return CreatedAtAction(nameof(GetSnapshotById), new { coin = chain.Coin, network = chain.Network, id = snapshot.Id },
            SnapshotResponse.From(snapshot));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<SnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotResponse>> GetSnapshotById(string coin, string network, [Range(1, long.MaxValue)] long id,
        CancellationToken token)
    {
        var snapshot = await getSnapshot.HandleAsync(new(ResolveNetwork(coin, network), id), token);
        return snapshot is null ? NotFound() : Ok(SnapshotResponse.From(snapshot));
    }

    [HttpGet]
    [ProducesResponseType<SnapshotHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotHistoryResponse>> GetSnapshotHistory(string coin, string network,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery, StringLength(512)] string? cursor = null, CancellationToken token = default)
    {
        var page = await getHistory.HandleAsync(new(ResolveNetwork(coin, network), pageSize, cursor), token);
        return Ok(new SnapshotHistoryResponse(page.Items.Select(SnapshotResponse.From).ToArray(), page.NextCursor));
    }

    private static BlockchainNetwork ResolveNetwork(string coin, string network) => BlockchainNetwork.FindSupportedNetwork(coin, network)
        ?? throw new RequestValidationException("chain", "Unsupported currency/network combination. See GET /api/chains.");
}
