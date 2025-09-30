using RateLimitingGrpcService.Interceptor;
using RateLimitingGrpcService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();

builder.Services.AddGrpc(c => c.Interceptors.Add<RateLimitingInterceptor>());

var app = builder.Build();

app.MapGrpcService<RateLimiterService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. ");
app.Run();

public partial class Program { }  // added for testing
