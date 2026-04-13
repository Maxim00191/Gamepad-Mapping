namespace GamepadMapperGUI.Models;

public static class ControllerMappingOverlayLabelModeParser
{
    public static ControllerMappingOverlayPrimaryLabelMode Parse(string? s) =>
        s?.Trim().Equals("physicalControl", StringComparison.OrdinalIgnoreCase) == true
            ? ControllerMappingOverlayPrimaryLabelMode.PhysicalControl
            : ControllerMappingOverlayPrimaryLabelMode.ActionSummary;

    public static string ToSettingString(ControllerMappingOverlayPrimaryLabelMode mode) =>
        mode == ControllerMappingOverlayPrimaryLabelMode.PhysicalControl
            ? "physicalControl"
            : "actionSummary";
}
