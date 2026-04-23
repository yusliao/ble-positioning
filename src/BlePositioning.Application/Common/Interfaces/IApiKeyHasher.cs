namespace BlePositioning.Application.Common.Interfaces;

public interface IApiKeyHasher
{
    string Hash(string plaintextApiKey);
}
