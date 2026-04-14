namespace GamepadMapperGUI.Models;

public static class ControllerMappingOverlayLabelModeParser
{
    public static ControllerMappingOverlayPrimaryLabelMode Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return ControllerMappingOverlayPrimaryLabelMode.ActionSummary;
        var t = s.Trim();
        if (t.Equals("physicalControl", StringComparison.OrdinalIgnoreCase))
            return ControllerMappingOverlayPrimaryLabelMode.PhysicalControl;
        if (t.Equals("actionAndPhysical", StringComparison.OrdinalIgnoreCase))
            return ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl;
        return ControllerMappingOverlayPrimaryLabelMode.ActionSummary;
    }

    public static string ToSettingString(ControllerMappingOverlayPrimaryLabelMode mode) =>
        mode switch
        {
            ControllerMappingOverlayPrimaryLabelMode.PhysicalControl => "physicalControl",
            ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl => "actionAndPhysical",
            _ => "actionSummary"
        };
}
