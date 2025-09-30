using System.Net.Mime;
using Client.DTO;
using Client.Services;
using Microsoft.AspNetCore.Mvc;
using RateLimitingGrpcService;

namespace Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ConfigResourcesController : ControllerBase
{
    private readonly IRateLimiterGrpcService _grpcService;
    public ConfigResourcesController(IRateLimiterGrpcService grpcService)
    {
       _grpcService = grpcService;
    }


    // POST: RateLimitController/Create
    // 400BadRequest
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfigResponseDto>> Post([FromBody] ConfigRequestDto? configRequest)
    {
        if (configRequest == null)
        {
            return BadRequest();
        }

        var configInput = new ConfigureResourceRequest()
        {
            Resource = configRequest.ResourceId,
            MaxRequests = configRequest.MaxRequests,
            WindowSeconds = configRequest.WindowSeconds
        };

        var config = await _grpcService.ConfigureAsync(configInput, CancellationToken.None);

        var response = new ConfigResponseDto() {Success = config.Success};

        return Ok(response);
    }
}
