#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationVisionAlgorithmCatalog
{
    public static IReadOnlyList<AutomationNodeInlineChoiceOption> FindImageAlgorithmChoiceOptions() =>
    [
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.YoloOnnx,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_YoloOnnx"
        },
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.TemplateMatch,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_TemplateMatch"
        },
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.OpenCvTemplateMatch,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_OpenCvTemplateMatch"
        },
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.ColorThreshold,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_ColorThreshold"
        },
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.TextRegion,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_TextRegion"
        },
        new()
        {
            StoredValue = AutomationVisionAlgorithmStorage.Contour,
            LabelResourceKey = "AutomationVisionAlgorithm_Option_Contour"
        }
    ];
}
