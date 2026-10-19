using Blockchain.Api.Contracts;
using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api/snapshot-collections")]
public sealed class SnapshotCollectionsController(
    ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>> captureAll) : ControllerBase
{
    // The response describes the completed collection, including per-network failures.
    [HttpPost]
    [EnableRateLimiting("capture")]
    [ProducesResponseType<IReadOnlyList<NetworkCaptureResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<NetworkCaptureResponse>>> CaptureAllSnapshots(CancellationToken token)
    {
        var results = await captureAll.HandleAsync(new(), token);
        return Ok(results.Select(NetworkCaptureResponse.From).ToArray());
    }
}
