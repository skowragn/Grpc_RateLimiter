# Rate Limiter with Grpc service
This solution contains a standalone ASP.NET Core 9 gRPC service. It provides the Grpc API to rate limiting that other microservices can call to check rate limits.
___

## 1. gRPC API Definition
```protobuf
syntax = "proto3";

service RateLimiter {
    rpc CheckRateLimit(RateLimitRequest) returns (RateLimitResponse);
    rpc ConfigureResource(ConfigureResourceRequest) returns (ConfigureResourceResponse);
}

message RateLimitRequest {
    string client_id = 1;
    string resource = 2;
}

message RateLimitResponse {
    bool allowed = 1;
}

message ConfigureResourceRequest {
    string resource = 1;
    int32 max_requests = 2;
    int32 window_seconds = 3;
}

message ConfigureResourceResponse {
    bool success = 1;
}
```
___
# Requirements
## 1. Core Requirements

1.  **Implement Fixed Window rate limiting algorithm**     
    -   Track requests within time windows
    -   Reset counters when window expires
2.  **Support configuration per resource type**
    -   Resources must be configured via the ConfigureResource RPC before use
    -   Different resources should have different rate limits
    -   Configuration should be dynamically updatable
3.  **Thread-safe implementation**
    -   Must handle high-concurrency scenarios
    -   No race conditions
    ___
## 2. Technical Constraints

-   **Language:** C#
-   **You may use:** Any NuGet packages you find helpful
-   **Do not use:** Existing rate limiting libraries 
-   Persistence is not required for this activity.
___
# Solution

## Assumptions 
**Based on the requirements:**
1. Rate Limiter middleware
  
<img width="902" height="634" alt="image" src="https://github.com/user-attachments/assets/aef0bb15-1cba-41eb-b6f9-8452ecb25d1c" />
                                       Figure 1. Rate Limiting Service as a middleware API gateway 
                                       
Note: The backend resources routing in case of allowed results has been not implemented (out of scope) 


2. Fixed window counter algorithms
3. The gRPC methods here are unary calls - request message as a parameter, and returns the response (similar to actions on web API controllers).

## Fixed window counter algorithm
Fixed window counter algorithm works as follows:
- The algorithm divides the timeline into fix-sized time windows and assign a counter for each window.
- Each request increments the counter by one.
- Once the counter reaches the pre-defined threshold (max requests), new requests are dropped until a new time window starts.

**Example:** window time is 1 second and the system allows a maximum of 3 requests per second. In each second window, if more than 3 requests are received, extra requests are dropped.

- To implement this solution, we need a counter to track the number of requests sent from the same client. If the counter exceeds the limit, the request is rejected.
- We also need to save configuration data for each resource (MaxRequests, WindowSeconds).
- For this solution, it's best to use an in-memory cache because it's fast (RedisDB or Azure Redis Cache). A database isn't a good solution here because it has slow disk access.

## Design decisions and trade-offs
### 1. Cache
The DistributedMemoryCache service (with MemoryCache implemention - in-memory cache) has been selected to store client counter and resource data. 
It could be also used ConcurrentDictionary<string,object> which is faster, in case when we do not want to remove anything from cache.

The MemoryCache is not an actual distributed cache. 
Cached items are stored by the app instance on the server where the app is running as regular .NET objects.

With distributed caching servers like Redis, the cache is located on a different machine, often a dedicated one, and is shared among multiple servers. 
A distributed cache does not store .NET objects but binary or text (often JSON or XML) data. 
Therefore, .NET objects should be serialized before being stored in the cache, and deserialized after being retrieved.
With MemoryCache, serialization and deserialization is not necessary.

If we have several instances of our current implemention of RateLimitingService , there will be several in-memory caches, each in a different machine. One node may not be aware of the changes done by the second node, and even if the database is consistent, it will not get to the database because of its own local cache. In that case users/clients seeing diffrent data from diffrent servers.

Program.cs
```
builder.Services.AddDistributedMemoryCache();
```
Next step:  Replacement of the MemoryCache with some Distributed Cache for example e.g. RedisDB / Azure Redis Cache.


### 2. RateLimitingGrpcService
The RateLimiterService service (\RateLimitingGrpcService\Services\ **RateLimiterService.cs** ) has the main implemention of the **CheckRateLimit** and **ConfigureResource** methods from **ratelimiter.proto** file:

```
service RateLimiterService {
    rpc CheckRateLimit(RateLimitRequest) returns (RateLimitResponse);
    rpc ConfigureResource(ConfigureResourceRequest) returns (ConfigureResourceResponse);
}
```

The ratelimit algorithm is located in the **RateLimitingInterceptor** class (\RateLimitingGrpcService\Interceptor\ **RateLimitingInterceptor.cs** ).

It is dedicated for grpc service solution from **Grpc.Core.Interceptors.Interceptor** which is called before grpc methods from **RateLimiterService** (it works in the similar way as the ASP.NET Core Web Api Middleware, but additionally it has an input parameters from request).

**Based on proto file and rpc method definition:**
- The unary grpc methods are needed for our solution. It is simpler to implement than stream methods.

\RateLimitingGrpcService\Program.cs
```
builder.Services.AddGrpc(c => c.Interceptors.Add<RateLimitingInterceptor>());
```
\RateLimitingGrpcService\Interceptor\ **RateLimitingInterceptor.cs

**UnaryServerHandler method**
1.	Check if the called endpoint is ConfigureResource. If so, the input data from current request is cached.
2.	The result is saved in the
```
httpContext.Features.Set(new ConfigureResourceResponse { Success = false/true });
```
 and ConfigureResource API from RateLimiterService is called via continuation(request, context) with stored result in context.
3. Check if the called endpoint is CheckRateLimit. The client and resource data is fetched from cache and ratelimit check is executed.
```
private static bool IsRateLimitCompleted(ClientStatistics? clientStatistics, LimitRequests? resourceLimits)
 {
     return clientStatistics != null && resourceLimits != null && DateTime.UtcNow <
            clientStatistics.LastSuccessfulResponseTime.AddSeconds(resourceLimits.TimeWindow) &&
            clientStatistics.NumberOfRequestsCompletedSuccessfully == resourceLimits.MaxRequests;
 }
```
4. Based on the result next client call is allowed or disallowed.
   The result is saved in the
```
 httpContext.Features.Set(new RateLimitResponse { Allowed = false/true });
```
 and CheckRateLimit API from RateLimiterService is called via continuation(request, context) with stored result in context.

### 3. How to build and run the solution
1. Please as a first build the **RateLimitingGrpcService.csproj**
2. The following files should be generated by the protocol buffer compiler: **Ratelimiter.cs** and **RatelimiterGrpc.cs** (\RateLimitingGrpcService\obj\Debug\net9.0\Protos)
   They are required to use the rpc types (RateLimitRequest, RateLimitResponse, RateLimiterServiceBase, RateLimiterServiceClient etc.) in RateLimiterService, Client (REST Web API) and Tests solutions.

### Note 1: 
please check if the following part is in the **RateLimitingGrpcService.csproj** before build: 
```
<ItemGroup>
     <Protobuf Include="Protos\ratelimiter.proto" GrpcServices="Both" />
 </ItemGroup>
```
3. Then you can use the REST API Client (ASP.NET Core 9 Web API with calling grpc method via Grpc Client)
The Grpc Methods are called with the **Grpc Client** (from **Grpc.Net.Client** library):
**Client.csproj** 
```
    <ItemGroup>
        <Protobuf Include="..\RateLimitingGrpcService\Protos\ratelimiter.proto" GrpcServices="Client" />
    </ItemGroup>
```
**\Client\Extensions\ServiceCollectionExtension.cs**
```
services.AddGrpcClient<RateLimitingGrpcService.RateLimiterService.RateLimiterServiceClient>(client =>
 {
     client.Address = new Uri("https://localhost:7285");
 });

services.AddScoped<IRateLimiterGrpcService, RateLimiterGrpcService>();
```

**\Client\Services\RateLimiterGrpcService.cs**
```
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
```

Please run both RateLimitingGrpcService and Client:

Swagger from Client:
```
https://localhost:7290/swagger
```
<img width="641" height="517" alt="image" src="https://github.com/user-attachments/assets/e3da5631-fe7a-4a88-94ed-8e6b96295413" />

### Note 2: 
The **RateLimitingGrpcService.Test** is using **Grpc.Core.Testing** library, but is has not been finished yet.


### 4. Future fixes
-  Replace of the DistributedMemoryCache service usage with the distributed cache for example Redis
  Note: The docker containers should be used for RateLimitingGrpcService and docker container with RedisDB for development and  RedisDB/Azure Redis Cache for production
-  Global error handling with Interceptor
-  Testing - e.g. RateLimitingInterceptor unit/integration tests; improvement of the current tests samples
-  More logging
-  Refactoring/Code clean up
  
-  Consider to implement ratelimiter for grpc stream methods (with partitions)
-  For Cloud solution with one-time setting in the configuration file, the Azure Api Management should be considered
  (Grpc API with APIM: https://learn.microsoft.com/en-us/azure/api-management/grpc-api?tabs=portal and https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/concepts/implement-rate-limiting-azure-api-management)

### 5. Design decisions and trade-offs

### 6.Performance considerations
1. grpc proto buffer - binary communication
2. async/await from ASP.NET Core 
3. in-memory cache or distributed cache to store client and resource data  
