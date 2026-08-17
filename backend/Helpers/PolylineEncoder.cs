using System.Text;

namespace backend.Helpers;

public static class PolylineEncoder
{
    public static string EncodePolyline6(
        IEnumerable<(double Latitude, double Longitude)> coordinates)
    {
        var result = new StringBuilder();
        long previousLatitude = 0;
        long previousLongitude = 0;

        foreach (var coordinate in coordinates)
        {
            var latitude = (long)Math.Round(
                coordinate.Latitude * 1_000_000,
                MidpointRounding.AwayFromZero);
            var longitude = (long)Math.Round(
                coordinate.Longitude * 1_000_000,
                MidpointRounding.AwayFromZero);

            EncodeValue(latitude - previousLatitude, result);
            EncodeValue(longitude - previousLongitude, result);
            previousLatitude = latitude;
            previousLongitude = longitude;
        }

        return result.ToString();
    }

    private static void EncodeValue(long value, StringBuilder result)
    {
        var encoded = value < 0 ? ~(value << 1) : value << 1;
        while (encoded >= 0x20)
        {
            result.Append((char)((0x20 | (encoded & 0x1f)) + 63));
            encoded >>= 5;
        }

        result.Append((char)(encoded + 63));
    }
}
