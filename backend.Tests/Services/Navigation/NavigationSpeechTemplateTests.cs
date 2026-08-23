using backend.Services.Navigation;

namespace backend.Tests.Services.Navigation;

public sealed class NavigationSpeechTemplateTests
{
    [Fact]
    public void Normalize_DynamicDistanceWithoutToken_UsesSafeTemplateFallback()
    {
        var context = new NavigationSpeechContext(
            "Continue", "WalkingToDestination", "WALK",
            DistanceMeters: 500, UseDynamicDistance: true);

        var normalized = NavigationSpeechTemplate.Normalize(
            "Sige, lakad pa tayo nang 500m.", context);

        Assert.Contains(NavigationSpeechTemplate.DistanceToken, normalized);
        Assert.DoesNotContain("500m", normalized);
    }

    [Fact]
    public void Render_ReplacesDistanceTokenWithCurrentBucketedDistance()
    {
        var rendered = NavigationSpeechTemplate.Render(
            "Sige, lakad pa tayo nang {distance}.", 147);

        Assert.Equal("Sige, lakad pa tayo nang 150m.", rendered);
    }

    [Fact]
    public void Normalize_NonDynamicInstructionRejectsUnexpectedDistanceToken()
    {
        var context = new NavigationSpeechContext(
            "TurnRight", "WalkingToDestination", "WALK",
            DistanceMeters: 250, UseDynamicDistance: false);

        var normalized = NavigationSpeechTemplate.Normalize(
            "Kanan tayo after {distance}.", context);

        Assert.Equal("Turn right here.", normalized);
    }
}
