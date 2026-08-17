using backend.Helpers;

namespace backend.Tests.Helpers;

public sealed class PolylineEncoderTests
{
    [Fact]
    public void EncodePolyline6_RoundTripsCoordinatesThroughDecoder()
    {
        (double Latitude, double Longitude)[] points =
        [
            (15.16970191080015, 120.58775806157624),
            (15.153593638458716, 120.59199321409343),
            (15.137020841985489, 120.58648839309423)
        ];

        var encoded = PolylineEncoder.EncodePolyline6(points);
        var decoded = PolylineDecoder.DecodePolyline6(encoded);

        Assert.Equal(points.Length, decoded.Count);
        for (var index = 0; index < points.Length; index++)
        {
            Assert.Equal(points[index].Latitude, decoded[index].Latitude, 6);
            Assert.Equal(points[index].Longitude, decoded[index].Longitude, 6);
        }
    }
}
