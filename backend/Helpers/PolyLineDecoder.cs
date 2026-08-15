namespace backend.Helpers;

public static class PolylineDecoder
{
    public static List<(double Latitude, double Longitude)> DecodePolyline6(
        string encoded)
    {
        var coordinates = new List<(double Latitude, double Longitude)>();

        int index = 0;
        int latitude = 0;
        int longitude = 0;

        while (index < encoded.Length)
        {
            // Decode latitude
            int result = 0;
            int shift = 0;
            int b;

            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1F) << shift;
                shift += 5;
            }
            while (b >= 0x20);

            int deltaLatitude =
                (result & 1) != 0
                    ? ~(result >> 1)
                    : (result >> 1);

            latitude += deltaLatitude;

            // Decode longitude
            result = 0;
            shift = 0;

            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1F) << shift;
                shift += 5;
            }
            while (b >= 0x20);

            int deltaLongitude =
                (result & 1) != 0
                    ? ~(result >> 1)
                    : (result >> 1);

            longitude += deltaLongitude;

            coordinates.Add((
                latitude / 1_000_000.0,
                longitude / 1_000_000.0
            ));
        }

        return coordinates;
    }
}