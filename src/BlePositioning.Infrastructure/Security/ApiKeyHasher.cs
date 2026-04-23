using System.Security.Cryptography;
using System.Text;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace BlePositioning.Infrastructure.Security;

public sealed class ApiKeyHasher(IOptions<ApiKeyOptions> options) : IApiKeyHasher
{
    public string Hash(string plaintextApiKey)
    {
        var pepper = options.Value.Pepper ?? "";
        var bytes = Encoding.UTF8.GetBytes(pepper + plaintextApiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
