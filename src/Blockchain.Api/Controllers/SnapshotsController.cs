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
[Route("api/networks/{coin}/{network}/snapshots")]
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
        var snapshot = await capture.HandleAsync(new(coin, network), token);
        return CreatedAtAction(nameof(GetSnapshotById), new { coin = snapshot.BlockchainNetwork.Cryptocurrency.Code,
            network = snapshot.BlockchainNetwork.Code, id = snapshot.Id },
            SnapshotResponse.From(snapshot));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<SnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotResponse>> GetSnapshotById(string coin, string network, long id,
        CancellationToken token)
    {
        var snapshot = await getSnapshot.HandleAsync(new(coin, network, id), token);
        return snapshot is null ? NotFound() : Ok(SnapshotResponse.From(snapshot));
    }

    [HttpGet]
    [ProducesResponseType<SnapshotHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SnapshotHistoryResponse>> GetSnapshotHistory(string coin, string network,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken token = default)
    {
        var result = await getHistory.HandleAsync(new(coin, network, page, pageSize), token);
        return Ok(new SnapshotHistoryResponse(result.Items.Select(SnapshotResponse.From).ToArray(),
            result.Page, result.PageSize, result.HasNextPage));
    }

}
