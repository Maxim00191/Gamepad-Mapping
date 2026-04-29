#nullable enable

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationVisionAlgorithmComposition
{
    public static AutomationVisionPipeline CreateDefaultPipeline()
    {
        var openCvTemplateMatcher = new AutomationOpenCvTemplateMatcher();
        var visionTemplate = new AutomationTemplateMatchVisionAlgorithm(openCvTemplateMatcher);
        var visionOpenCvTemplate = new OpenCvTemplateMatchVisionAlgorithm(openCvTemplateMatcher);
        var visionYoloOnnx = new AutomationYoloOnnxVisionAlgorithm();
        var visionThreshold = new AutomationColorThresholdVisionAlgorithm();
        var visionContour = new AutomationContourVisionAlgorithm(visionThreshold);
        var visionTextRegion = new AutomationTextRegionVisionAlgorithm();

        return new AutomationVisionPipeline(
            [visionTemplate, visionOpenCvTemplate, visionYoloOnnx, visionThreshold, visionContour, visionTextRegion]);
    }
}
