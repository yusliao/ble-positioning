using System.Security.Claims;
using System.Text.Encodings.Web;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Application.Devices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BlePositioning.API.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string DeviceIdClaimType = "device_id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
            return AuthenticateResult.NoResult();

        var plaintext = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(plaintext))
            return AuthenticateResult.NoResult();

        await using var scope = scopeFactory.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IApiKeyHasher>();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackedDeviceRepository>();
        var hash = hasher.Hash(plaintext);
        var device = await repository.GetByApiKeyHashAsync(hash, Context.RequestAborted);
        if (device is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new[]
        {
            new Claim(DeviceIdClaimType, device.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
