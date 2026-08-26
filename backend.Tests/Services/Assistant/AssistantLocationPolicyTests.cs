using backend.Services.Assistant;

namespace backend.Tests.Services.Assistant;

public sealed class AssistantLocationPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FreshAccurateFix_IsCurrentAndUsableForReroute()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            Now.AddSeconds(-30),
            Now);

        Assert.Equal(AssistantLocationPolicy.Current, result.Reliability);
        Assert.Equal(30d, result.AgeSeconds!.Value);
        Assert.True(result.CanUseForReroute);
    }

    [Fact]
    public void ThirtyOneSecondFix_IsLastKnownAndContextOnly()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            Now.AddSeconds(-31),
            Now);

        Assert.Equal(AssistantLocationPolicy.LastKnown, result.Reliability);
        Assert.Equal(31d, result.AgeSeconds!.Value);
        Assert.False(result.CanUseForReroute);
    }

    [Fact]
    public void SixtySecondFix_IsStillLastKnownButNotReroutable()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            Now.AddSeconds(-60),
            Now);

        Assert.Equal(AssistantLocationPolicy.LastKnown, result.Reliability);
        Assert.False(result.CanUseForReroute);
    }

    [Fact]
    public void OlderThanSixtySeconds_IsStale()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            Now.AddSeconds(-61),
            Now);

        Assert.Equal(AssistantLocationPolicy.Stale, result.Reliability);
        Assert.False(result.CanUseForReroute);
    }

    [Fact]
    public void PoorAccuracy_IsNeverUsableForReroute()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            76,
            Now.AddSeconds(-5),
            Now);

        Assert.Equal(AssistantLocationPolicy.Inaccurate, result.Reliability);
        Assert.False(result.CanUseForReroute);
    }

    [Fact]
    public void MissingTimestamp_IsUnknownInsteadOfPretendingCoordinatesAreCurrent()
    {
        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            null,
            Now);

        Assert.Equal(AssistantLocationPolicy.Unknown, result.Reliability);
        Assert.Null(result.AgeSeconds);
        Assert.False(result.CanUseForReroute);
    }

    [Fact]
    public void PersistedUnspecifiedTimestamp_IsTreatedAsUtc()
    {
        var persistedTimestamp = DateTime.SpecifyKind(
            Now.AddSeconds(-12),
            DateTimeKind.Unspecified);

        var result = AssistantLocationPolicy.Assess(
            15.1,
            120.5,
            20,
            persistedTimestamp,
            Now);

        Assert.Equal(AssistantLocationPolicy.Current, result.Reliability);
        Assert.Equal(12d, result.AgeSeconds!.Value);
        Assert.True(result.CanUseForReroute);
    }

    [Fact]
    public void MissingCoordinates_AreUnavailable()
    {
        var result = AssistantLocationPolicy.Assess(
            null,
            null,
            null,
            null,
            Now);

        Assert.Equal(AssistantLocationPolicy.Unavailable, result.Reliability);
        Assert.False(result.CanUseForReroute);
    }
}
