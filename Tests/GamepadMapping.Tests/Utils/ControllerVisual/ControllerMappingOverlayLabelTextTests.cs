using Gamepad_Mapping.Utils.ControllerVisual;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class ControllerMappingOverlayLabelTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  a\n\tb  ", "a b")]
    [InlineData("Line1\r\nLine2", "Line1 Line2")]
    public void NormalizeForOverlay_Collapses_whitespace(string? input, string expected) =>
        Assert.Equal(expected, ControllerMappingOverlayLabelText.NormalizeForOverlay(input));

    [Fact]
    public void Estimate_AfterNormalize_MatchesSingleLineMetrics()
    {
        var normalized = ControllerMappingOverlayLabelText.NormalizeForOverlay("One\nTwo\nThree");
        var fromNormalized = ControllerMappingOverlayLabelSizeEstimator.Estimate(normalized, null);
        var fromPlain = ControllerMappingOverlayLabelSizeEstimator.Estimate("One Two Three", null);
        Assert.Equal(fromPlain.Width, fromNormalized.Width);
        Assert.Equal(fromPlain.Height, fromNormalized.Height);
    }
}
