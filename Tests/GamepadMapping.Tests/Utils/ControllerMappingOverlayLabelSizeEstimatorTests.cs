using Gamepad_Mapping.Utils.ControllerSvg;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class ControllerMappingOverlayLabelSizeEstimatorTests
{
    [Fact]
    public void Estimate_ShortPrimary_IsSmallerThan_LongPrimary()
    {
        var shortSize = ControllerMappingOverlayLabelSizeEstimator.Estimate("A", null);
        var longSize = ControllerMappingOverlayLabelSizeEstimator.Estimate("Hold Left Stick + Right Bumper", null);
        Assert.True(longSize.Width >= shortSize.Width);
        Assert.True(longSize.Height >= shortSize.Height);
    }

    [Fact]
    public void Estimate_WithSecondary_AddsWidth()
    {
        var primaryOnly = ControllerMappingOverlayLabelSizeEstimator.Estimate("Action", null);
        var withSecondary = ControllerMappingOverlayLabelSizeEstimator.Estimate("Action", "+2");
        Assert.True(withSecondary.Width >= primaryOnly.Width);
    }
}
