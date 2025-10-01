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

    Persistence is not required for this activity.
___
# Solution
# 1. Context

## Assumptions 
**Based on the requirements:**
1. Rate Limiter should be middleware
  
<img width="1170" height="820" alt="image" src="https://github.com/user-attachments/assets/daffc576-91fa-4229-9da9-0803a2df7c38" />

Figure 1. Rate Limiting Service as a middleware API gateway (with MemoryCache)

2. Fixed window counter algorithms should be used
3. The gRPC methods here are unary calls (based on proto file) - request message as a parameter, and returns the response (similar to actions on web API controllers).

## Fixed window counter algorithm
Fixed window counter algorithm works as follows:
- The algorithm divides the timeline into fix-sized time windows and assign a counter for each window.
- Each request increments the counter by one.
- Once the counter reaches the pre-defined threshold (max requests), new requests are dropped until a new time window starts.

**Example:** Window time is 1 second and the system allows a maximum of 3 requests per second. In each second window, if more than 3 requests are received, extra requests are dropped.

- To implement this solution, a counter to track the number of requests, sent from the same client, is needed. If the counter exceeds the limit, the request is rejected.
- In addition, configuration data for each resource (MaxRequests, WindowSeconds) is needed to save.


## Design decisions and trade-offs
### 1. Single Server Environment
#### Performance
1. grpc protobuf - binary communication faster compared to REST
2. Asynchronous calls in client apps
 ```C#
await _grpcClient.CheckRateLimitAsync(limitRequest, cancellationToken: cancellationToken);
```
3. async/await from ASP.NET Core 
4. in-memory cache or distributed cache to store client and resource data

##### Cache
- The best solution here, it will be to use in-memory cache to store required data. A database storage is not a good choice because it has slow disk access. 
- The MemoryDistributedCache has been selected to store client counter and resource data as a first step. 
- The MemoryDistributedCache implements IDistributedCache and stores items in memory. 
- It allows for implementing a true distributed caching solution in the future if multiple nodes or fault tolerance become necessary.
- ConcurrentDictionary<string,object> could be a faster solution, but if nothing needs to be removed from the cache. It runs the risk of memory exhaustion.
- MemoryDistributedCache implements MemoryCache and MemoryCache is using ConcurrentDictionary, thus thread-safe.
- MemoryCache is very fast, though not the fastest cache. However, it provides a good balance between functionality and performance.
- A distributed cache (Redis) stores binary or text (often JSON or XML) data. Therefore, .NET objects should be serialized before being stored in the cache, and deserialized after being retrieved (performance).
- With MemoryCache, serialization and deserialization is not necessary.

**\RateLimitingGrpcService\Program.cs**
```C#
builder.Services.AddDistributedMemoryCache();
```
### 2. Multi Server Environment
#### Scalability and Reliability
Scaling the system to support multiple servers and concurrent threads.

##### Synchronization issue
- The MemoryDistributedCache is not an actual distributed cache. Cached items are stored by the app instance on the server where the app is running as regular .NET objects.
- With distributed caching servers like Redis, the cache is located on a different machine, often a dedicated one, and is shared among multiple servers.
- If the RateLimitingService have several instances of the current implemention, there will be several in-memory caches, each in a different machine.
  One node may not be aware of the changes done by the second node, In that case users/clients seeing different data from different servers.
  
- Instead of the MemoryDistributedCache/MemoryCache per each RateLimitingService instance hosted on multi-server/nodes, a better approach is to use centralized data stores like Redis/Azure Redis Cache.

<img width="1190" height="975" alt="image" src="https://github.com/user-attachments/assets/c1172a29-680a-4dd6-99a4-2d5d173ee1e8" />
Figure 3. Rate Limiting Service as a middleware API gateway (with Distributed Cache - Redis)

**\RateLimitingGrpcService\Program.csProgram.cs**
```C#
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["ConnectionString:Redis"];
    options.InstanceName = "Instance";
});
```
##### Race condition
- If two requests concurrently read the client counter value before either of them writes the value back, each will increment the counter by one and write it back without checking the other thread. 
- It could be used locks as the most obvious solution for solving race condition (Pessimistic Locking). However, locks will significantly slow down the system. 
- If the Redis is selected as distributed cache - provides strategies to solve the problem for example sorted sets data structure, which should be used.

# 2. Current Implementation
The solution consists of:
- **RateLimitingGrpcService** - ASP.NET Core 9 grpc service
- **Client** - ASP.NET Core 9 Web API (it uses gRpc Client to call the unary methods from the RateLimitingGrpcService)
- **RateLimitingGrpcService** - Tests (under construction)
  
## 1. RateLimitingGrpcService
The RateLimiterService service (\RateLimitingGrpcService\Services\ **RateLimiterService.cs** ) has the main implemention of the **CheckRateLimit** and **ConfigureResource** methods from **ratelimiter.proto** file:

```C#
service RateLimiterService {
    rpc CheckRateLimit(RateLimitRequest) returns (RateLimitResponse);
    rpc ConfigureResource(ConfigureResourceRequest) returns (ConfigureResourceResponse);
}
```
## 2. Interceptor
The ratelimit algorithm is located in the **RateLimitingInterceptor** class (\RateLimitingGrpcService\Interceptor\ **RateLimitingInterceptor.cs** ).

It is dedicated for grpc service solution from **Grpc.Core.Interceptors.Interceptor** which is called before grpc methods from **RateLimiterService** (it works in the similar way as the ASP.NET Core Web Api Middleware, but additionally it has an input parameters from request).

**Based on proto file and rpc method definition:**
- The unary grpc methods are needed for this solution. It is simpler to implement than the streaming methods.

**\RateLimitingGrpcService\Program.cs**
```C#
builder.Services.AddGrpc(c => c.Interceptors.Add<RateLimitingInterceptor>());
```
**\RateLimitingGrpcService\Interceptor\ RateLimitingInterceptor.cs**

## 3. Rate Limit Flow
**UnaryServerHandler method - basic flow**
1.	Check if the called endpoint is ConfigureResource. If so, the input data from current request is cached.
2.	The result is saved in the httpContext.Features:
```
httpContext.Features.Set(new ConfigureResourceResponse { Success = false/true });
```
 and ConfigureResource API from RateLimiterService is called via continuation(request, context) with stored result in context.
 
3. Check if the called endpoint is CheckRateLimit. The client and resource data is fetched from cache and ratelimit check is executed.

```C#
private static bool IsRateLimitCompleted(ClientStatistics? clientStatistics, LimitRequests? resourceLimits)
{
   return clientStatistics != null && resourceLimits != null && DateTime.UtcNow < clientStatistics.LastSuccessfulResponseTime.AddSeconds(resourceLimits.TimeWindow) &&
          clientStatistics.NumberOfRequestsCompletedSuccessfully == resourceLimits.MaxRequests;
}
```

4. Based on the result next client call is allowed or disallowed.

   The result is saved in the

``` C#
 httpContext.Features.Set(new RateLimitResponse { Allowed = false/true });
```
and CheckRateLimit API from RateLimiterService is called via continuation(request, context) with stored result in context.


## 4. How to build and run the solution
1. Please as a first build the **RateLimitingGrpcService.csproj**
2. The following files should be generated by the protocol buffer compiler: **Ratelimiter.cs** and **RatelimiterGrpc.cs** (**\RateLimitingGrpcService\obj\Debug\net9.0\Protos**)
   They are required to use the rpc types (RateLimitRequest, RateLimitResponse, RateLimiterServiceBase, RateLimiterServiceClient etc.) in RateLimiterService, Client (REST Web API) and Tests solutions.

### Note 1: 
Please check if the following part is in the **RateLimitingGrpcService.csproj** before build: 

```C#
<ItemGroup>
     <Protobuf Include="Protos\ratelimiter.proto" GrpcServices="Both" />
 </ItemGroup>
```

3. Then you can use the REST API Client (ASP.NET Core 9 Web API with calling grpc method via Grpc Client)
The Grpc Methods are called with the **Grpc Client** (from **Grpc.Net.Client** library):

**Client.csproj** 

```C#
    <ItemGroup>
        <Protobuf Include="..\RateLimitingGrpcService\Protos\ratelimiter.proto" GrpcServices="Client" />
    </ItemGroup>
```
**\Client\Extensions\ServiceCollectionExtension.cs**

```C#
services.AddGrpcClient<RateLimitingGrpcService.RateLimiterService.RateLimiterServiceClient>(client =>
 {
     client.Address = new Uri("https://localhost:5001");
 });

services.AddScoped<IRateLimiterGrpcService, RateLimiterGrpcService>();
```

**\Client\Services\RateLimiterGrpcService.cs**

```C#
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

```C#
https://localhost:7290/swagger
```
<img width="641" height="517" alt="image" src="https://github.com/user-attachments/assets/e3da5631-fe7a-4a88-94ed-8e6b96295413" />
 Figure 2. Client Swagger    

### Note 2: 
The **RateLimitingGrpcService.Test** is using **Grpc.Core.Testing** library.


## 5. Future fixes
1. Scalability and Reliability - Exchange the current usage of the MemoryDistributedCache with the real distributed cache with for example Redis/Azure Redis Cache.
   Implement access to real distibuted cache - Redis with strategies sorted sets data structure and/or add Optimistic Locking.
2. Refactoring/Code clean up the current solution
3. Add Global error handling with Interceptor
4. Testing - e.g. RateLimitingInterceptor unit/integration tests; improvement of the current tests samples
5. More logging, tracing
6. Authentication and Authorization (with OIDC/OAuth 2.0/2.1 (IdentityServer, AAD etc.) and Interceptor)
7. Consider to implement RateLimiter with grpc streaming methods - e.g. gRPC bidirectional streaming can be used to replace unary gRPC calls in high-performance scenarios.

```C#
rpc BookCatalogStream (stream BookCatalogRequest) returns (stream BookCatalogResponse);
service RateLimiter {
    rpc CheckRateLimit(stream RateLimitRequest) returns (stream RateLimitResponse);
    rpc ConfigureResource(stream ConfigureResourceRequest) returns (stream ConfigureResourceResponse);
}
```
8. Better usage/more effective of grpc Client for Clients - Reuse gRPC channels: https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-9.0 
9. For Cloud solution with one-time setting in the configuration file, the Azure Api Management should be considered
  (Grpc API with APIM: https://learn.microsoft.com/en-us/azure/api-management/grpc-api?tabs=portal and https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/concepts/implement-rate-limiting-azure-api-management)

