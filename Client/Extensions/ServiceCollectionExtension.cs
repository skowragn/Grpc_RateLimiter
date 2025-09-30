using Client.Services;

namespace Client.Extensions;

public static class ServiceCollectionExtension
{
    public static void AddGrpcSdk(this IServiceCollection services)
    { 
         services.AddGrpcClient<RateLimitingGrpcService.RateLimiterService.RateLimiterServiceClient>(client =>
         {
             client.Address = new Uri("https://localhost:5001");
         });

        services.AddScoped<IRateLimiterGrpcService, RateLimiterGrpcService>();
    }

}
