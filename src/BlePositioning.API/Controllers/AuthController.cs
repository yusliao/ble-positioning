using BlePositioning.API.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using BlePositioning.API.Options;
using BlePositioning.API.Security;
using BlePositioning.Application.Common.Models;
using BlePositioning.Application.Security;
using BlePositioning.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlePositioning.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(RateLimitingServiceExtensions.GeneralPolicyName)]
public sealed class AuthController(
    IOptions<DevAdminOptions> devAdmin,
    IOptions<JwtOptions> jwtOptions,
    JwtTokenIssuer jwt) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public ActionResult<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var opt = devAdmin.Value;
        if (!TryResolveAccount(opt, request.Username, request.Password, out var subject, out var role))
            return Problem(title: "Unauthorized", statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid credentials.");

        var token = jwt.CreateAccessToken(subject, role);
        var expires = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes);
        var data = new LoginResponse(token, expires, role);
        return Ok(new ApiResponse<LoginResponse>(true, data, null, HttpContext.GetTraceId()));
    }

    private static bool TryResolveAccount(
        DevAdminOptions opt,
        string? username,
        string? password,
        out string subject,
        out string role)
    {
        subject = "";
        role = "";

        if (opt.Users is { Count: > 0 })
        {
            foreach (var u in opt.Users)
            {
                if (!string.Equals(username, u.Username, StringComparison.Ordinal) || password != u.Password)
                    continue;
                subject = u.Username;
                role = MapRoleName(u.Role);
                return !string.IsNullOrEmpty(subject);
            }

            return false;
        }

        if (!string.Equals(username, opt.Username, StringComparison.Ordinal) || password != opt.Password)
            return false;

        subject = opt.Username;
        role = BlePositioningRoles.Admin;
        return true;
    }

    private static string MapRoleName(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return BlePositioningRoles.Viewer;
        if (string.Equals(configured, BlePositioningRoles.Admin, StringComparison.Ordinal))
            return BlePositioningRoles.Admin;
        if (string.Equals(configured, BlePositioningRoles.Viewer, StringComparison.Ordinal))
            return BlePositioningRoles.Viewer;
        return BlePositioningRoles.Viewer;
    }
}

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, string Role);
