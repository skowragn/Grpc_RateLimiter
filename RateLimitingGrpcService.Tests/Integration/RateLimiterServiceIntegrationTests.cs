using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RateLimitingGrpcService.Tests.Integration;

public class RateLimiterServiceIntegrationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task WhenCheckRateLimitAsyncIsCalled_ThenItShouldReturnAllowed()
    {
        // Arrange
        GrpcChannelOptions options = new() { HttpHandler = _factory.Server.CreateHandler() };
        GrpcChannel channel = GrpcChannel.ForAddress(_factory.Server.BaseAddress, options);
        RateLimiterService.RateLimiterServiceClient client = new(channel);

        var expectedResponse = true;
        var request = new RateLimitRequest { Resource = "api/people", ClientId = "Aga" };

        // Act
        var response = await client.CheckRateLimitAsync(request);

        // Assert
       Assert.Equal(expectedResponse, response.Allowed);
    }
}
