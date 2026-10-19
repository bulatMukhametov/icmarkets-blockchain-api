using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ChainsController(
    ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>> captureAll) : ControllerBase
{
    [HttpGet("chains")]
    [ProducesResponseType<IReadOnlyList<BlockchainNetwork>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<BlockchainNetwork>> GetChains() => Ok(BlockchainNetwork.SupportedNetworks);

    // Synchronous collection: 200 describes the completed report, including per-chain failures.
    [HttpPost("snapshot-collections")]
    [EnableRateLimiting("capture")]
    [ProducesResponseType<IReadOnlyList<NetworkCaptureResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<NetworkCaptureResult>>> CaptureAllSnapshots(CancellationToken token) =>
        Ok(await captureAll.HandleAsync(new(), token));
}
