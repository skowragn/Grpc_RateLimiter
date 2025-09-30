using Microsoft.Extensions.Caching.Distributed;

namespace RateLimitingGrpcService.Extensions;

public static class DistributedCachingExtensions
{
    public static async Task SetCacheValueAsync<T>(this IDistributedCache distributedCache, string key, T value, CancellationToken token = default)
    {
        if (value != null) await distributedCache.SetAsync(key, value: value.ToByteArray()!, token: token);
    }

    public static async Task<T?> GetCacheValueAsync<T>(this IDistributedCache distributedCache, string key, CancellationToken token = default) where T : class
    {
        var result = await distributedCache.GetAsync(key, token);
        return result.FromByteArray<T>();
    }
}

