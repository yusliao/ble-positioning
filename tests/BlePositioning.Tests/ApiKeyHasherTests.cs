using System.Security.Cryptography;
using System.Text;
using BlePositioning.Application.Common.Interfaces;
using BlePositioning.Infrastructure.Options;
using BlePositioning.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BlePositioning.Tests;

public sealed class ApiKeyHasherTests
{
    private static IApiKeyHasher Create(string pepper) =>
        new ApiKeyHasher(Options.Create(new ApiKeyOptions { Pepper = pepper }));

    [Fact]
    public void Hash_Is_Deterministic()
    {
        var h = Create("p");
        Assert.Equal(h.Hash("abc"), h.Hash("abc"));
    }

    [Fact]
    public void Hash_Matches_Sha256_Of_Pepper_Plus_Utf8Plain()
    {
        const string pepper = "test-pepper";
        const string plain = "raw-key";
        var h = Create(pepper);
        var expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(pepper + plain))).ToLowerInvariant();
        Assert.Equal(expected, h.Hash(plain));
    }

    [Fact]
    public void Different_Plain_Produces_Different_Hash()
    {
        var h = Create("x");
        Assert.NotEqual(h.Hash("a"), h.Hash("b"));
    }
}
