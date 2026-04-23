namespace BlePositioning.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "BlePositioning";
    public string Audience { get; set; } = "BlePositioning";
    public string SigningKey { get; set; } = "CHANGE_ME_MINIMUM_32_CHARACTERS_LONG!!";
    public int AccessTokenMinutes { get; set; } = 60;
}
