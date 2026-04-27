namespace GamepadMapperGUI.Models;

public static class ControllerMappingOverlayLabelModeParser
{
    public const string ActionSummarySettingValue = "actionSummary";
    public const string PhysicalControlSettingValue = "physicalControl";
    public const string ActionAndPhysicalSettingValue = "actionAndPhysical";

    public static ControllerMappingOverlayPrimaryLabelMode DefaultMode =>
        ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl;

    public static string DefaultSettingValue => ActionAndPhysicalSettingValue;

    public static ControllerMappingOverlayPrimaryLabelMode Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return DefaultMode;
        var t = s.Trim();
        if (t.Equals(PhysicalControlSettingValue, StringComparison.OrdinalIgnoreCase))
            return ControllerMappingOverlayPrimaryLabelMode.PhysicalControl;
        if (t.Equals(ActionAndPhysicalSettingValue, StringComparison.OrdinalIgnoreCase))
            return ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl;
        if (t.Equals(ActionSummarySettingValue, StringComparison.OrdinalIgnoreCase))
            return ControllerMappingOverlayPrimaryLabelMode.ActionSummary;
        return DefaultMode;
    }

    public static string ToSettingString(ControllerMappingOverlayPrimaryLabelMode mode) =>
        mode switch
        {
            ControllerMappingOverlayPrimaryLabelMode.PhysicalControl => PhysicalControlSettingValue,
            ControllerMappingOverlayPrimaryLabelMode.ActionAndPhysicalControl => ActionAndPhysicalSettingValue,
            _ => ActionSummarySettingValue
        };
}
