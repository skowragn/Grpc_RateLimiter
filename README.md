# Rate Limiter with Grpc service
# Goal: 
Build a standalone gRPC rate limiting service that other microservices can call to check rate limits.
___

# Requirements

## 1. gRPC Service Definition

Create a `.proto` file defining your service:

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

## 2. Core Requirements

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
## 3. Technical Constraints

-   **Language:** C#
-   **You may use:** Any NuGet packages you find helpful
-   **Do not use:** Existing rate limiting libraries 
-   Persistence is not required for this activity.
    ___
    -   How to build and run the solution
    -   Design decisions and trade-offs
    -   What you would improve with more time
    -   Any assumptions made
    -   Performance considerations
