using backend.Models.Valhalla;
using backend.Services.Routing;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class RouteGeneratorServiceTests
{
    [Fact]
    public async Task GenerateAsync_RoutesEveryConsecutivePairAndJoinsGeometryInOrder()
    {
        var valhalla = new Mock<IValhallaService>();
        var calls = new List<(double FromLatitude, double ToLatitude)>();
        valhalla
            .Setup(service => service.GetRouteAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                "auto",
                It.IsAny<CancellationToken>()))
            .Callback<double, double, double, double, string, CancellationToken>(
                (fromLatitude, _, toLatitude, _, _, _) => calls.Add((fromLatitude, toLatitude)))
            .ReturnsAsync((double fromLatitude, double fromLongitude, double toLatitude,
                double toLongitude, string _, CancellationToken _) => new ValhallaRouteResponse
            {
                Trip = new ValhallaTrip
                {
                    Legs =
                    [
                        new ValhallaLeg
                        {
                            Points =
                            [
                                [fromLongitude, fromLatitude],
                                [(fromLongitude + toLongitude) / 2, (fromLatitude + toLatitude) / 2],
                                [toLongitude, toLatitude]
                            ]
                        }
                    ]
                }
            });

        var service = new RouteGeneratorService(valhalla.Object);
        var result = await service.GenerateAsync(
            [[15.1, 120.1], [15.2, 120.2], [15.3, 120.3]]);

        Assert.Equal([(15.1, 15.2), (15.2, 15.3)], calls);
        Assert.Equal(5, result.Count);
        Assert.Equal([15.1, 120.1], result[0]);
        Assert.Equal([15.2, 120.2], result[2]);
        Assert.Equal([15.3, 120.3], result[4]);
    }

    [Fact]
    public async Task GenerateAsync_WhenAValhallaSegmentHasNoGeometry_ThrowsWithSegmentIndex()
    {
        var valhalla = new Mock<IValhallaService>();
        valhalla
            .Setup(service => service.GetRouteAsync(
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                It.IsAny<double>(),
                "auto",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValhallaRouteResponse { Trip = new ValhallaTrip() });

        var service = new RouteGeneratorService(valhalla.Object);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync([[15.1, 120.1], [15.2, 120.2]]));

        Assert.Contains("0->1", exception.Message);
    }
}
