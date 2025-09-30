using RateLimitingGrpcService;

namespace Client.Services;

public interface IRateLimiterGrpcService
{
    Task<RateLimitResponse> CheckRateLimitAsync(RateLimitRequest request, CancellationToken cancellationToken);
    Task<ConfigureResourceResponse> ConfigureAsync(ConfigureResourceRequest configureRequest, CancellationToken cancellationToken);

}