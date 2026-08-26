using backend.Models.Database;
using backend.Models.TricyclePointManagement;
using backend.Repositories;
using backend.Services.Transportation;
using Moq;

namespace backend.Tests.Services.Transportation;

public sealed class AdminTricyclePointManagementServiceTests
{
    [Fact]
    public async Task GetDuplicateWarningsAsync_ReturnsActiveAndArchivedNearbyPointsAndExcludesCurrentPoint()
    {
        var repository = new Mock<ITricyclePointRepository>(MockBehavior.Strict);
        var pointService = new Mock<ITricyclePointService>(MockBehavior.Strict);
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Point(1, "A", 15.145000, 120.588000, true),
                Point(2, "B", 15.145200, 120.588000, false),
                Point(3, "C", 15.150000, 120.588000, true)
            ]);
        var service = new AdminTricyclePointManagementService(repository.Object, pointService.Object);

        var result = await service.GetDuplicateWarningsAsync(
            15.145000,
            120.588000,
            excludeTricyclePointId: 1,
            thresholdMeters: 75);

        var warning = Assert.Single(result);
        Assert.Equal(2, warning.TricyclePointId);
        Assert.False(warning.IsActive);
        Assert.InRange(warning.DistanceMeters, 20, 30);
    }

    [Fact]
    public async Task CreateAsync_WhenNearbyPointExists_ReturnsWarningButStillCreatesPoint()
    {
        var repository = new Mock<ITricyclePointRepository>(MockBehavior.Strict);
        var pointService = new Mock<ITricyclePointService>(MockBehavior.Strict);
        repository.Setup(item => item.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Point(1, "EXISTING", 15.145100, 120.588000, true)]);
        pointService.Setup(item => item.AddVerifiedTricyclePointAsync(
                "NEW-01", "New TODA", 15.145000, 120.588000, 500,
                null, null, null, null, null, null, null, null, null, true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TricyclePointMutationResult.Success(
                Point(5, "NEW-01", 15.145000, 120.588000, true, "New TODA")));
        var service = new AdminTricyclePointManagementService(repository.Object, pointService.Object);

        var result = await service.CreateAsync(new AdminTricyclePointMutationRequest
        {
            PointCode = "NEW-01",
            PointName = "New TODA",
            Latitude = 15.145000,
            Longitude = 120.588000,
            RadiusMeters = 500
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Single(result.Response.DuplicateWarnings);
        Assert.Equal(5, result.Response.Point.TricyclePointId);
    }

    [Fact]
    public async Task SetActiveAsync_WhenArchiving_PreservesRecordAndSetsInactive()
    {
        var repository = new Mock<ITricyclePointRepository>(MockBehavior.Strict);
        var pointService = new Mock<ITricyclePointService>(MockBehavior.Strict);
        var point = Point(7, "ARCHIVE", 15.145000, 120.588000, true);
        repository.Setup(item => item.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(point);
        repository.Setup(item => item.UpdateAsync(
                It.Is<TricyclePoint>(candidate => candidate.TricyclePointId == 7 && !candidate.IsActive),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TricyclePoint candidate, CancellationToken _) => candidate);
        var service = new AdminTricyclePointManagementService(repository.Object, pointService.Object);

        var result = await service.SetActiveAsync(7, false);

        Assert.True(result.Succeeded);
        Assert.False(result.Response!.Point.IsActive);
        repository.VerifyAll();
    }

    private static TricyclePoint Point(
        long id,
        string code,
        double latitude,
        double longitude,
        bool isActive,
        string? name = null) => new()
    {
        TricyclePointId = id,
        PointCode = code,
        PointName = name ?? code,
        CenterLatitude = latitude,
        CenterLongitude = longitude,
        RadiusMeters = 500,
        IsActive = isActive,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
