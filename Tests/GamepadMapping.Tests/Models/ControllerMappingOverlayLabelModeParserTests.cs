using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Models;

public class ControllerMappingOverlayLabelModeParserTests
{
    [Theory]
    [InlineData(null, ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)]
    [InlineData("", ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)]
    [InlineData("actionSummary", ControllerMappingOverlayPrimaryLabelMode.ActionSummary)]
    [InlineData("physicalControl", ControllerMappingOverlayPrimaryLabelMode.PhysicalControl)]
    [InlineData("PHYSICALCONTROL", ControllerMappingOverlayPrimaryLabelMode.PhysicalControl)]
    [InlineData("actionAndPhysical", ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)]
    [InlineData("ACTIONANDPHYSICAL", ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)]
    [InlineData("unexpectedValue", ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl)]
    public void Parse_maps_expected_mode(string? raw, ControllerMappingOverlayPrimaryLabelMode expected) =>
        Assert.Equal(expected, ControllerMappingOverlayLabelModeParser.Parse(raw));

    [Theory]
    [InlineData(ControllerMappingOverlayPrimaryLabelMode.ActionSummary, "actionSummary")]
    [InlineData(ControllerMappingOverlayPrimaryLabelMode.PhysicalControl, "physicalControl")]
    [InlineData(ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl, "actionAndPhysical")]
    public void ToSettingString_round_trips(ControllerMappingOverlayPrimaryLabelMode mode, string s) =>
        Assert.Equal(s, ControllerMappingOverlayLabelModeParser.ToSettingString(mode));
}
