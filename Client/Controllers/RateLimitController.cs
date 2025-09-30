using System.Net.Mime;
using Client.Services;
using Microsoft.AspNetCore.Mvc;
using Client.DTO;
using RateLimitingGrpcService;

namespace Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RateLimitController : ControllerBase
{
    private readonly IRateLimiterGrpcService _grpcService;

    public RateLimitController(IRateLimiterGrpcService grpcService)
    {
        _grpcService = grpcService;
    }


    // POST: RateLimitController/Create
    // 400BadRequest
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RateLimitResponseDto>> Post([FromBody] RateLimitRequestDto? rateLimitRequest)
    {
        if (rateLimitRequest == null)
        {
            return BadRequest();
        }

        var rateLimitInput = new RateLimitRequest
        {
            ClientId = rateLimitRequest.ClientId,
            Resource = rateLimitRequest.Resource
        };

        var responseRateLimiter = await _grpcService.CheckRateLimitAsync(rateLimitInput, CancellationToken.None);
        var response = new RateLimitResponseDto { Allowed = responseRateLimiter.Allowed };

        return Ok(response);
    }
}