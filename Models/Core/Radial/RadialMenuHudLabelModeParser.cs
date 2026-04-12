namespace GamepadMapperGUI.Models;

public static class RadialMenuHudLabelModeParser
{
    public static RadialMenuHudLabelMode Parse(string? s) =>
        s?.Trim().ToLowerInvariant() switch
        {
            "descriptiononly" => RadialMenuHudLabelMode.DescriptionOnly,
            "keyboardkeyonly" or "keyonly" => RadialMenuHudLabelMode.KeyboardKeyOnly,
            _ => RadialMenuHudLabelMode.Both
        };

    public static string ToSettingString(RadialMenuHudLabelMode mode) =>
        mode switch
        {
            RadialMenuHudLabelMode.DescriptionOnly => "descriptionOnly",
            RadialMenuHudLabelMode.KeyboardKeyOnly => "keyboardKeyOnly",
            _ => "both"
        };
}
