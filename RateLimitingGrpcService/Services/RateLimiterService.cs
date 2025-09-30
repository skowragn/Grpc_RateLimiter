using Grpc.Core;

namespace RateLimitingGrpcService.Services;
public class RateLimiterService(ILogger<RateLimiterService> logger) : RateLimitingGrpcService.RateLimiterService.RateLimiterServiceBase
{
    public override Task<RateLimitResponse> CheckRateLimit(RateLimitRequest request, ServerCallContext context)
    {
        var limitResult = context.GetHttpContext().Features.Get<RateLimitResponse>();

        if (limitResult == null) return Task.FromResult(new RateLimitResponse());
        logger.LogInformation("CheckRateLimit endpoint with result from RateLimitingInterceptor:{RateLimitResponse}", limitResult);
        return Task.FromResult(limitResult);
    }

    public override Task<ConfigureResourceResponse> ConfigureResource(ConfigureResourceRequest request, ServerCallContext context)
    {
        var configResult = context.GetHttpContext().Features.Get<ConfigureResourceResponse>();
        
        if (configResult == null) return Task.FromResult(new ConfigureResourceResponse());
        logger.LogInformation(" ConfigureResource endpoint with result from RateLimitingInterceptor:{ConfigResourceResponse}", configResult);
        return Task.FromResult(configResult);
    }
}