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
    IBlockchainNetworkCatalog catalog,
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
        var chain = await ResolveNetworkAsync(coin, network, token);
        var snapshot = await capture.HandleAsync(new(chain), token);
        return CreatedAtAction(nameof(GetSnapshotById), new { coin = chain.Cryptocurrency.Code, network = chain.Code, id = snapshot.Id },
            SnapshotResponse.From(snapshot));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<SnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotResponse>> GetSnapshotById(string coin, string network, [Range(1, long.MaxValue)] long id,
        CancellationToken token)
    {
        var snapshot = await getSnapshot.HandleAsync(new(await ResolveNetworkAsync(coin, network, token), id), token);
        return snapshot is null ? NotFound() : Ok(SnapshotResponse.From(snapshot));
    }

    [HttpGet]
    [ProducesResponseType<SnapshotHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotHistoryResponse>> GetSnapshotHistory(string coin, string network,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery, StringLength(512)] string? cursor = null, CancellationToken token = default)
    {
        var page = await getHistory.HandleAsync(new(await ResolveNetworkAsync(coin, network, token), pageSize, cursor), token);
        return Ok(new SnapshotHistoryResponse(page.Items.Select(SnapshotResponse.From).ToArray(), page.NextCursor));
    }

    private async Task<BlockchainNetwork> ResolveNetworkAsync(string coin, string network, CancellationToken token) =>
        await catalog.FindAsync(coin, network, token)
        ?? throw new RequestValidationException("chain", "Unsupported currency/network combination. See GET /api/chains.");
}
