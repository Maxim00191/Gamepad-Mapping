#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationVisionAlgorithmStorage
{
    public const string TemplateMatch = "template";
    public const string ColorThreshold = "color_threshold";
    public const string Contour = "contour";
    public const string OpenCvTemplateMatch = "opencv_template";
    public const string YoloOnnx = "yolo_onnx";
    public const string TextRegion = "text_region";

    public static AutomationVisionAlgorithmKind ParseKind(string? raw)
    {
        var t = raw?.Trim();
        if (string.Equals(t, ColorThreshold, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.ColorThreshold;
        if (string.Equals(t, Contour, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.Contour;
        if (string.Equals(t, OpenCvTemplateMatch, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.OpenCvTemplateMatch;
        if (string.Equals(t, YoloOnnx, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.YoloOnnx;
        if (string.Equals(t, TextRegion, StringComparison.OrdinalIgnoreCase))
            return AutomationVisionAlgorithmKind.TextRegion;
        return AutomationVisionAlgorithmKind.TemplateMatch;
    }

    public static AutomationVisionAlgorithmKind ParseFindImageAlgorithmKind(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AutomationVisionAlgorithmKind.YoloOnnx;

        return ParseKind(raw);
    }

    public static string ToStorageValue(AutomationVisionAlgorithmKind kind) => kind switch
    {
        AutomationVisionAlgorithmKind.ColorThreshold => ColorThreshold,
        AutomationVisionAlgorithmKind.Contour => Contour,
        AutomationVisionAlgorithmKind.OpenCvTemplateMatch => OpenCvTemplateMatch,
        AutomationVisionAlgorithmKind.YoloOnnx => YoloOnnx,
        AutomationVisionAlgorithmKind.TextRegion => TextRegion,
        _ => TemplateMatch
    };
}
