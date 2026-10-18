using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ChainsController(
    ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<CaptureResult>> captureAll) : ControllerBase
{
    [HttpGet("chains")]
    [ProducesResponseType<IReadOnlyList<ChainId>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ChainId>> GetChains() => Ok(ChainId.Supported);

    // Synchronous collection: 200 describes the completed report, including per-chain failures.
    [HttpPost("snapshot-collections")]
    [EnableRateLimiting("capture")]
    [ProducesResponseType<IReadOnlyList<CaptureResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<CaptureResult>>> CaptureAll(CancellationToken token) =>
        Ok(await captureAll.HandleAsync(new(), token));
}
