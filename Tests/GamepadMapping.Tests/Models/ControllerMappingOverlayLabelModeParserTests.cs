using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Models;

public class ControllerMappingOverlayLabelModeParserTests
{
    [Theory]
    [InlineData(null, ControllerMappingOverlayPrimaryLabelMode.ActionSummary)]
    [InlineData("", ControllerMappingOverlayPrimaryLabelMode.ActionSummary)]
    [InlineData("actionSummary", ControllerMappingOverlayPrimaryLabelMode.ActionSummary)]
    [InlineData("physicalControl", ControllerMappingOverlayPrimaryLabelMode.PhysicalControl)]
    [InlineData("PHYSICALCONTROL", ControllerMappingOverlayPrimaryLabelMode.PhysicalControl)]
    public void Parse_maps_expected_mode(string? raw, ControllerMappingOverlayPrimaryLabelMode expected) =>
        Assert.Equal(expected, ControllerMappingOverlayLabelModeParser.Parse(raw));

    [Theory]
    [InlineData(ControllerMappingOverlayPrimaryLabelMode.ActionSummary, "actionSummary")]
    [InlineData(ControllerMappingOverlayPrimaryLabelMode.PhysicalControl, "physicalControl")]
    public void ToSettingString_round_trips(ControllerMappingOverlayPrimaryLabelMode mode, string s) =>
        Assert.Equal(s, ControllerMappingOverlayLabelModeParser.ToSettingString(mode));
}
