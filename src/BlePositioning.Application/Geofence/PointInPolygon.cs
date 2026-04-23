namespace BlePositioning.Application.Geofence;

/// <summary>平面多边形内判定（楼层米制；射线法 / 奇偶规则）。外环为闭合并与 <c>ZonePolygonValidator</c> 一致时适用。</summary>
public static class PointInPolygon
{
    public static bool RayCastingContains(IReadOnlyList<(double X, double Y)> ring, double x, double y)
    {
        if (ring.Count < 3)
            return false;

        var n = ring.Count;
        var inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var xi = ring[i].X;
            var yi = ring[i].Y;
            var xj = ring[j].X;
            var yj = ring[j].Y;
            if ((yi > y) == (yj > y))
                continue;
            var xCross = (xj - xi) * (y - yi) / (yj - yi + 1e-15) + xi;
            if (x < xCross)
                inside = !inside;
        }

        return inside;
    }
}
