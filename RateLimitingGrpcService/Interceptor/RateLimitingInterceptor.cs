using Grpc.Core;
using Microsoft.Extensions.Caching.Distributed;
using RateLimitingGrpcService.Extensions;
using RateLimitingGrpcService.Model;
using System.Net;

namespace RateLimitingGrpcService.Interceptor;
public class RateLimitingInterceptor(ILogger<RateLimitingInterceptor> logger, IDistributedCache cache) : Grpc.Core.Interceptors.Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            var requestPath = context.Method;
            var httpContext = context.GetHttpContext();

            if (HasRequestedPath(requestPath, "ConfigureResource") && request is ConfigureResourceRequest configRequest)
            {
                await UpdateResourceStorage(configRequest);
                logger.LogInformation(message: "Data for {ResourceData} updated", configRequest.Resource);
                httpContext.Features.Set(new ConfigureResourceResponse { Success = true });
            }
            else if (HasRequestedPath(requestPath, "CheckRateLimit") && request is RateLimitRequest limitRequest)
            {
                var clientKey = GenerateClientKey(limitRequest.ClientId, limitRequest.Resource);
                var clientStatistics = await GetClientStatisticsByKey(clientKey);

                var resourceKey = GenerateResourceKey(limitRequest.Resource);
                var resourceLimits = await GetResourceLimitByKey(resourceKey);
                
                if (IsRateLimitCompleted(clientStatistics, resourceLimits))
                {
                    httpContext.Features.Set(new RateLimitResponse { Allowed = false });
                    logger.LogInformation(message: nameof(HttpStatusCode.TooManyRequests));
                    return await continuation(request, context);
                }

                if (resourceLimits == null) return await continuation(request, context);

                await UpdateClientStatisticsStorage(clientKey, resourceLimits.MaxRequests);
                httpContext.Features.Set(new RateLimitResponse { Allowed = true });
            }
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Rate Limiting failed: {Error}", ex.Message);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Rate Limiting failed"));
        }
    }
    private async Task UpdateClientStatisticsStorage(string key, int maxRequests)
    {
        var clientStat = await cache.GetCacheValueAsync<ClientStatistics>(key);

        if (clientStat != null)
        {
            clientStat.LastSuccessfulResponseTime = DateTime.UtcNow;

            if (clientStat.NumberOfRequestsCompletedSuccessfully == maxRequests)
                clientStat.NumberOfRequestsCompletedSuccessfully = 1;
            else
                clientStat.NumberOfRequestsCompletedSuccessfully++;

            await cache.SetCacheValueAsync(key, clientStat);
        }
        else
        {
            var clientStatistics = new ClientStatistics
            {
                LastSuccessfulResponseTime = DateTime.UtcNow,
                NumberOfRequestsCompletedSuccessfully = 1
            };

            await cache.SetCacheValueAsync(key, clientStatistics);
        }
    }
    private async Task UpdateResourceStorage(ConfigureResourceRequest configRequest)
    {
        if (!string.IsNullOrEmpty(configRequest.Resource))
        {
            var resourceKey = GenerateResourceKey(configRequest.Resource);
            var limit = new LimitRequests { MaxRequests = configRequest.MaxRequests, TimeWindow = configRequest.WindowSeconds };

            await cache.SetCacheValueAsync(resourceKey, limit);
        }
    }
    private async Task<ClientStatistics?> GetClientStatisticsByKey(string key)
    {
        if (!string.IsNullOrEmpty(key))
            return await cache.GetCacheValueAsync<ClientStatistics>(key);
        return new ClientStatistics();
    }
    private async Task<LimitRequests?> GetResourceLimitByKey(string key)
    {
        return await cache.GetCacheValueAsync<LimitRequests>(key);
    }
    private static string GenerateClientKey(string clientId, string resource)
    { 
        if(!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(resource))
            return $"{clientId}_{resource}";
        return string.Empty;
    }
    private static string GenerateResourceKey(string resource) => $"{resource}";
    private static bool IsRateLimitCompleted(ClientStatistics? clientStatistics, LimitRequests? resourceLimits)
    {
       return clientStatistics != null && resourceLimits != null && DateTime.UtcNow < clientStatistics.LastSuccessfulResponseTime.AddSeconds(resourceLimits.TimeWindow) &&
              clientStatistics.NumberOfRequestsCompletedSuccessfully == resourceLimits.MaxRequests;
    }
    private static bool HasRequestedPath(string requestPath, string path) =>
        !string.IsNullOrEmpty(requestPath) &&
        !string.IsNullOrEmpty(path) &&
        requestPath.Contains(path, StringComparison.Ordinal);
}