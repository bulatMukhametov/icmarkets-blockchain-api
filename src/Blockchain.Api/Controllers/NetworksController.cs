using Blockchain.Api.Contracts;
using Blockchain.Application.Networks;
using Microsoft.AspNetCore.Mvc;

namespace Blockchain.Api.Controllers;

[ApiController]
[Route("api/chains")]
public sealed class NetworksController(IBlockchainNetworkCatalog catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BlockchainNetworkResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BlockchainNetworkResponse>>> GetNetworks(CancellationToken token)
    {
        var networks = await catalog.GetAllAsync(token);
        return Ok(networks.Select(network =>
            new BlockchainNetworkResponse(network.Cryptocurrency.Code, network.Code)).ToArray());
    }
}
