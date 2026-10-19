using Blockchain.Application.Abstractions;
using Blockchain.Application.Snapshots.CaptureAllSnapshots;
using Blockchain.Api.Contracts;
using Blockchain.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ChainsController(
    IBlockchainNetworkCatalog catalog,
    ICommandHandler<CaptureAllSnapshotsCommand, IReadOnlyList<NetworkCaptureResult>> captureAll) : ControllerBase
{
    [HttpGet("chains")]
    [ProducesResponseType<IReadOnlyList<BlockchainNetworkResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BlockchainNetworkResponse>>> GetChains(CancellationToken token) =>
        Ok((await catalog.GetAllAsync(token)).Select(x => new BlockchainNetworkResponse(x.Cryptocurrency.Code, x.Code)).ToArray());

    // Synchronous collection: 200 describes the completed report, including per-chain failures.
    [HttpPost("snapshot-collections")]
    [EnableRateLimiting("capture")]
    [ProducesResponseType<IReadOnlyList<NetworkCaptureResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<NetworkCaptureResult>>> CaptureAllSnapshots(CancellationToken token) =>
        Ok(await captureAll.HandleAsync(new(), token));
}
