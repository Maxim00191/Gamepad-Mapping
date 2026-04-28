#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationVisionAlgorithmRequirements
{
    public static bool RequiresNeedleImage(AutomationVisionAlgorithmKind kind) =>
        kind is AutomationVisionAlgorithmKind.TemplateMatch or AutomationVisionAlgorithmKind.OpenCvTemplateMatch;

    public static bool RequiresYoloOnnxModel(AutomationVisionAlgorithmKind kind) =>
        kind == AutomationVisionAlgorithmKind.YoloOnnx;

    public static bool UsesColorDetectionOptions(AutomationVisionAlgorithmKind kind) =>
        kind is AutomationVisionAlgorithmKind.ColorThreshold or AutomationVisionAlgorithmKind.Contour;

    public static bool UsesTextDetectionOptions(AutomationVisionAlgorithmKind kind) =>
        kind == AutomationVisionAlgorithmKind.TextRegion;
}
