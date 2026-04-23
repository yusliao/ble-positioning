using BlePositioning.Application.Geofence;

namespace BlePositioning.Tests;

public sealed class PointInPolygonTests
{
    private static readonly (double X, double Y)[] UnitSquare =
    [
        (0, 0), (10, 0), (10, 10), (0, 10), (0, 0),
    ];

    [Fact]
    public void Center_inside_square()
    {
        Assert.True(PointInPolygon.RayCastingContains(UnitSquare, 5, 5));
    }

    [Fact]
    public void Far_outside_square()
    {
        Assert.False(PointInPolygon.RayCastingContains(UnitSquare, 20, 20));
    }
}
