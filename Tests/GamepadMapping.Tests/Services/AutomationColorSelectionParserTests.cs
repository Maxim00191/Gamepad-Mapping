#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationColorSelectionParserTests
{
    [Fact]
    public void ApplyTargetHex_ValidHex_OverridesHsvRangeAndKeepsArea()
    {
        var baseline = new AutomationColorDetectionOptions(0, 10, 0, 10, 0, 10, 123);

        var result = AutomationColorSelectionParser.ApplyTargetHex(baseline, "#FF0000");

        Assert.Equal(123, result.MinimumAreaPx);
        Assert.InRange(result.HueMin, 160, 179);
        Assert.InRange(result.HueMax, 0, 20);
        Assert.True(result.SaturationMin >= 200);
        Assert.Equal(255, result.SaturationMax);
        Assert.True(result.ValueMin >= 200);
        Assert.Equal(255, result.ValueMax);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("#12")]
    [InlineData("#XYZ123")]
    public void ApplyTargetHex_InvalidInput_ReturnsBaseline(string? targetHex)
    {
        var baseline = new AutomationColorDetectionOptions(1, 2, 3, 4, 5, 6, 7);

        var result = AutomationColorSelectionParser.ApplyTargetHex(baseline, targetHex);

        Assert.Equal(baseline, result);
    }
}
