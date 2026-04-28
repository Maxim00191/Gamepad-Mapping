#nullable enable

using GamepadMapperGUI.Models.Automation;
using Xunit;

namespace GamepadMapping.Tests.Models;

public sealed class AutomationVisionAlgorithmStorageTests
{
    [Theory]
    [InlineData("template", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("TEMPLATE", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("color_threshold", AutomationVisionAlgorithmKind.ColorThreshold)]
    [InlineData("contour", AutomationVisionAlgorithmKind.Contour)]
    [InlineData("unknown", AutomationVisionAlgorithmKind.TemplateMatch)]
    [InlineData("", AutomationVisionAlgorithmKind.TemplateMatch)]
    public void ParseKind_maps_storage_strings(string raw, AutomationVisionAlgorithmKind expected) =>
        Assert.Equal(expected, AutomationVisionAlgorithmStorage.ParseKind(raw));

    [Fact]
    public void ParseKind_null_returns_template()
    {
        Assert.Equal(AutomationVisionAlgorithmKind.TemplateMatch, AutomationVisionAlgorithmStorage.ParseKind(null));
    }

    [Fact]
    public void ToStorageValue_round_trips_enum_values()
    {
        Assert.Equal(
            AutomationVisionAlgorithmStorage.TemplateMatch,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.TemplateMatch));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.ColorThreshold,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.ColorThreshold));
        Assert.Equal(
            AutomationVisionAlgorithmStorage.Contour,
            AutomationVisionAlgorithmStorage.ToStorageValue(AutomationVisionAlgorithmKind.Contour));
    }
}
