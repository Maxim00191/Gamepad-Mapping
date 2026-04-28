#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationVisionAlgorithmStorage
{
    public const string TemplateMatch = "template";
    public const string ColorThreshold = "color_threshold";
    public const string Contour = "contour";

    public static AutomationVisionAlgorithmKind ParseKind(string? raw)
    {
        if (string.Equals(raw?.Trim(), ColorThreshold, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.ColorThreshold;
        if (string.Equals(raw?.Trim(), Contour, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.Contour;
        return AutomationVisionAlgorithmKind.TemplateMatch;
    }

    public static string ToStorageValue(AutomationVisionAlgorithmKind kind) => kind switch
    {
        AutomationVisionAlgorithmKind.ColorThreshold => ColorThreshold,
        AutomationVisionAlgorithmKind.Contour => Contour,
        _ => TemplateMatch
    };
}
