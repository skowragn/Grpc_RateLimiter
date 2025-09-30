using Grpc.Core.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace RateLimitingGrpcService.Tests.Unit;

public class RateLimiterServiceUnitTests
{
    private readonly Mock<ILogger<Services.RateLimiterService>> _logger = new();
    private Services.RateLimiterService CreateRateLimiterService() => new(_logger.Object);

    [Fact]
    public async Task WhenCheckRateLimitAsyncIsCalled_ThenItShouldReturnNotAllowed()
    {
        // Arrange
        var service = CreateRateLimiterService();

        var expectedOutput = false;
        var request = new RateLimitRequest  { Resource = "api/people", ClientId = "CCC"};

        var peer = "localhost";
        
        var mockContext = TestServerCallContext.Create(
            method: "",
            host: "",
            deadline: DateTime.UtcNow.AddMinutes(30),
            requestHeaders: [],
            cancellationToken: CancellationToken.None,
            peer: peer,
            authContext: null,
            contextPropagationToken: null,
            writeHeadersFunc: null,
            writeOptionsGetter: null,
            writeOptionsSetter: null);

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(new RateLimitResponse { Allowed = false });
        mockContext.UserState["__HttpContext"] = httpContext;
         
        // Act
        var response = await service.CheckRateLimit(request, mockContext);
        
        // Assert
        Assert.Equal(expectedOutput, response.Allowed);
    }

    [Fact]
    public async Task WhenConfigureResourceAsyncIsCalled_ThenItShouldReturnNotAllowed()
    {
        // Arrange
        var service = CreateRateLimiterService();

        var expectedOutput = true;
        var request = new ConfigureResourceRequest { Resource = "api/people", MaxRequests = 4, WindowSeconds = 3};

        var peer = "localhost";
        var mockContext = TestServerCallContext.Create(
            method: "",
            host: "",
            deadline: DateTime.UtcNow.AddMinutes(30),
            requestHeaders: [],
            cancellationToken: CancellationToken.None,
            peer: peer,
            authContext: null,
            contextPropagationToken: null,
            writeHeadersFunc: null,
            writeOptionsGetter: null,
            writeOptionsSetter: null);

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set(new ConfigureResourceResponse { Success = true });
        mockContext.UserState["__HttpContext"] = httpContext;

        // Act
        var response = await service.ConfigureResource(request, mockContext);

        // Assert
        Assert.Equal(expectedOutput, response.Success);
    }
}
