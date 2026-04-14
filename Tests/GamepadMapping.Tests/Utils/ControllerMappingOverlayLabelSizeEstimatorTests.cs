using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerSvg;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class ControllerMappingOverlayLabelSizeEstimatorTests
{
    [Fact]
    public void Estimate_ShortPrimary_IsNarrowerThan_LongPrimary()
    {
        var shortSize = ControllerMappingOverlayLabelSizeEstimator.Estimate("A", null);
        var longSize = ControllerMappingOverlayLabelSizeEstimator.Estimate("Hold Left Stick + Right Bumper", null);
        Assert.True(shortSize.Width <= longSize.Width);
        Assert.True(longSize.Width <= ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth + 0.001);
        Assert.True(longSize.Height >= shortSize.Height);
    }

    [Fact]
    public void Estimate_WithSecondary_RespectsMaxLabelBoxWidth()
    {
        var primaryOnly = ControllerMappingOverlayLabelSizeEstimator.Estimate("Action", null);
        var withSecondary = ControllerMappingOverlayLabelSizeEstimator.Estimate("Action", "+2");
        Assert.True(primaryOnly.Width <= ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth + 0.001);
        Assert.True(withSecondary.Width <= ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth + 0.001);
    }

    [Fact]
    public void Estimate_Item_Stacked_IsTallerThan_Inline_WhenSecondaryPresent()
    {
        var inline = ControllerMappingOverlayLabelSizeEstimator.Estimate(new ControllerMappingOverlayItem
        {
            PrimaryLabel = "Jump",
            SecondaryLabel = "A",
            StackPrimaryAndSecondary = false
        });
        var stacked = ControllerMappingOverlayLabelSizeEstimator.Estimate(new ControllerMappingOverlayItem
        {
            PrimaryLabel = "Jump",
            SecondaryLabel = "A",
            StackPrimaryAndSecondary = true
        });
        Assert.True(stacked.Height >= inline.Height);
    }

    [Fact]
    public void Estimate_VeryLongText_StaysWithinMaxLabelBox()
    {
        var veryLongText = new string('W', 100);
        var size = ControllerMappingOverlayLabelSizeEstimator.Estimate(veryLongText, null);
        Assert.InRange(size.Width, ControllerMappingOverlayLabelMetrics.MaxTextBlockWidth, ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth);
    }

    [Fact]
    public void Estimate_EmptyLabels_ReturnsMinimumSize()
    {
        var size = ControllerMappingOverlayLabelSizeEstimator.Estimate("", null);
        Assert.True(size.Width >= 1d);
        Assert.True(size.Height >= 1d);
    }

    [Fact]
    public void Estimate_InlineWithBothLabelsAtMaxWidth_StaysWithinMaxLabelBox()
    {
        var longPrimary = new string('M', 50);
        var longSecondary = new string('M', 50);
        var size = ControllerMappingOverlayLabelSizeEstimator.Estimate(longPrimary, longSecondary);
        Assert.InRange(size.Width, ControllerMappingOverlayLabelMetrics.MaxTextBlockWidth, ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth);
    }

    [Fact]
    public void Estimate_StackedMode_StaysWithinMaxLabelBox()
    {
        var item = new ControllerMappingOverlayItem
        {
            PrimaryLabel = new string('W', 80),
            SecondaryLabel = new string('W', 80),
            StackPrimaryAndSecondary = true
        };
        var size = ControllerMappingOverlayLabelSizeEstimator.Estimate(item);
        Assert.InRange(size.Width, ControllerMappingOverlayLabelMetrics.MaxTextBlockWidth, ControllerMappingOverlayLabelMetrics.MaxLabelBoxWidth);
    }
}
