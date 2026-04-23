using BlePositioning.Application.Floors;

namespace BlePositioning.Tests;

public sealed class ZonePolygonValidatorTests
{
    private const string ValidClosed =
        """{"type":"Polygon","coordinates":[[[0,0],[2,0],[2,2],[0,2],[0,0]]]}""";

    [Fact]
    public void Valid_polygon_returns_null()
    {
        Assert.Null(ZonePolygonValidator.Validate(ValidClosed));
    }

    [Fact]
    public void Open_ring_fails()
    {
        var err = ZonePolygonValidator.Validate(
            """{"type":"Polygon","coordinates":[[[0,0],[2,0],[2,2],[0,2]]]}""");
        Assert.NotNull(err);
    }

    [Fact]
    public void Wrong_type_fails()
    {
        var err = ZonePolygonValidator.Validate(
            """{"type":"Point","coordinates":[0,0]}""");
        Assert.NotNull(err);
    }

    [Fact]
    public void Invalid_json_fails()
    {
        var err = ZonePolygonValidator.Validate("{");
        Assert.NotNull(err);
    }
}
