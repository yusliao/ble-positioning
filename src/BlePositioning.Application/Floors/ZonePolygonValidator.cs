using System.Text.Json;

namespace BlePositioning.Application.Floors;

/// <summary>验证 <c>zone_polygon</c> 为 **平面包络线度单位** 的 GeoJSON <c>Polygon</c> 子集（与阶段 C 点在内判定一致的第一环）。</summary>
public static class ZonePolygonValidator
{
    public const int MaxLength = 100_000;
    public const int NameMaxLength = 100;
    private const double CloseEpsilon = 1e-6;

    public static string? Validate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Zone polygon is required.";

        var s = raw.Trim();
        if (s.Length > MaxLength)
            return "Zone polygon exceeds maximum length.";

        try
        {
            using var doc = JsonDocument.Parse(s);
            return ValidateRoot(doc.RootElement);
        }
        catch (JsonException)
        {
            return "Zone polygon is not valid JSON.";
        }
    }

    private static string? ValidateRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return "Zone polygon must be a JSON object.";

        if (!root.TryGetProperty("type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String
            || !string.Equals(typeEl.GetString(), "Polygon", StringComparison.Ordinal))
        {
            return "Zone polygon type must be \"Polygon\".";
        }

        if (!root.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Array)
            return "Zone polygon must include a \"coordinates\" array.";

        if (coords.GetArrayLength() < 1)
            return "Polygon must include at least one linear ring (exterior).";

        var ring0 = coords[0];
        if (ring0.ValueKind != JsonValueKind.Array)
            return "Exterior ring must be an array of positions.";

        var n = ring0.GetArrayLength();
        if (n < 4)
            return "Exterior ring must have at least 4 [x,y] positions (closed GeoJSON ring).";

        for (var i = 0; i < n; i++)
        {
            var pt = ring0[i];
            if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2)
                return "Each position must be a [x,y] array (floor plane, meters).";
            if (pt[0].ValueKind != JsonValueKind.Number || pt[1].ValueKind != JsonValueKind.Number)
                return "x and y must be numbers.";
            var x = pt[0].GetDouble();
            var y = pt[1].GetDouble();
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                return "x and y must be finite numbers.";
        }

        var first = ring0[0];
        var last = ring0[n - 1];
        var x0 = first[0].GetDouble();
        var y0 = first[1].GetDouble();
        var x1 = last[0].GetDouble();
        var y1 = last[1].GetDouble();
        if (Math.Abs(x0 - x1) > CloseEpsilon || Math.Abs(y0 - y1) > CloseEpsilon)
            return "Exterior ring must be closed (first and last [x,y] must match within tolerance).";

        return null;
    }
}
