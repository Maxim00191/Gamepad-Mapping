#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationVisionAlgorithmComposition
{
    public static AutomationVisionPipeline CreateDefaultPipeline(IAutomationTemplateMatcher templateMatcher)
    {
        ArgumentNullException.ThrowIfNull(templateMatcher);

        var visionTemplate = new AutomationTemplateMatchVisionAlgorithm(templateMatcher);
        var visionOpenCvTemplate = new OpenCvTemplateMatchVisionAlgorithm();
        var visionYoloOnnx = new AutomationYoloOnnxVisionAlgorithm();
        var visionThreshold = new AutomationColorThresholdVisionAlgorithm();
        var visionContour = new AutomationContourVisionAlgorithm(visionThreshold);
        var visionTextRegion = new AutomationTextRegionVisionAlgorithm();

        return new AutomationVisionPipeline(
            [visionTemplate, visionOpenCvTemplate, visionYoloOnnx, visionThreshold, visionContour, visionTextRegion]);
    }
}
