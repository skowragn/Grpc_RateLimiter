using Grpc.Core;
using RateLimitingGrpcService;

namespace Client.Services
{
    public class RateLimiterGrpcService : IRateLimiterGrpcService
    {
        private readonly RateLimiterService.RateLimiterServiceClient _grpcClient;
        
        public RateLimiterGrpcService(RateLimiterService.RateLimiterServiceClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        public async Task<RateLimitResponse> CheckRateLimitAsync(RateLimitRequest limitRequest, CancellationToken cancellationToken)
        {
            try
            {
                return await _grpcClient.CheckRateLimitAsync(limitRequest, cancellationToken: cancellationToken);
            }
            catch (RpcException ex)
            {
                throw ex;
            }
        }

        public async Task<ConfigureResourceResponse> ConfigureAsync(ConfigureResourceRequest configureRequest, CancellationToken cancellationToken)
        {
            try
            {
                return await _grpcClient.ConfigureResourceAsync(configureRequest, cancellationToken: cancellationToken);
     
            }
            catch (RpcException ex)
            {
                throw ex;
            }
        }
    }
}
