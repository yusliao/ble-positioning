using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BlePositioning.API.Security;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options)
{
    public string CreateAccessToken(string subject, string role, IReadOnlyDictionary<string, string>? extraClaims = null)
    {
        var opt = options.Value;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };
        if (extraClaims is not null)
        {
            foreach (var kv in extraClaims)
                claims.Add(new Claim(kv.Key, kv.Value));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(opt.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            opt.Issuer,
            opt.Audience,
            claims,
            expires: expires,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
