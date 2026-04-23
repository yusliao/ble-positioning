namespace BlePositioning.Admin.Options;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>例如 http://localhost:5230 或 http://api:8080</summary>
    public string Base { get; set; } = "http://localhost:5230";
}
