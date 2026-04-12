namespace GamepadMapperGUI.Models;

public static class ThumbstickDeadzoneShapeParser
{
    public static ThumbstickDeadzoneShape Parse(string? raw) =>
        string.Equals(raw?.Trim(), "radial", StringComparison.OrdinalIgnoreCase)
            ? ThumbstickDeadzoneShape.Radial
            : ThumbstickDeadzoneShape.Axial;

    public static string ToSettingString(ThumbstickDeadzoneShape shape) =>
        shape == ThumbstickDeadzoneShape.Radial ? "radial" : "axial";
}
