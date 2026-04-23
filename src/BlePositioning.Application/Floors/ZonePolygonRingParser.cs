using System.Text.Json;

namespace BlePositioning.Application.Floors;

/// <summary>从与 <see cref="ZonePolygonValidator"/> 一致的 <c>Polygon</c> JSON 中取出外环顶点（用于点内判定）。</summary>
public static class ZonePolygonRingParser
{
    public static bool TryGetExteriorRing(string zonePolygonJson, out IReadOnlyList<(double X, double Y)> ring)
    {
        ring = Array.Empty<(double, double)>();
        if (ZonePolygonValidator.Validate(zonePolygonJson) is not null)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(zonePolygonJson.Trim());
            if (!doc.RootElement.TryGetProperty("coordinates", out var coords) || coords.ValueKind != JsonValueKind.Array
                || coords.GetArrayLength() < 1)
                return false;

            var r0 = coords[0];
            if (r0.ValueKind != JsonValueKind.Array)
                return false;

            var n = r0.GetArrayLength();
            var list = new List<(double, double)>(n);
            for (var i = 0; i < n; i++)
            {
                var pt = r0[i];
                if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2)
                    return false;
                if (pt[0].ValueKind != JsonValueKind.Number || pt[1].ValueKind != JsonValueKind.Number)
                    return false;
                list.Add((pt[0].GetDouble(), pt[1].GetDouble()));
            }

            ring = list;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
