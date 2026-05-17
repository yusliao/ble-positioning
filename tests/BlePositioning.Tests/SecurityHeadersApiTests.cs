using System.Net;

namespace BlePositioning.Tests;

public sealed class SecurityHeadersApiTests : IClassFixture<ApiWebAppFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersApiTests(ApiWebAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_includes_security_headers()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
