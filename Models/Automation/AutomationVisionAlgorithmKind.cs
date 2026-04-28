#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public enum AutomationVisionAlgorithmKind
{
    TemplateMatch = 0,
    ColorThreshold = 1,
    Contour = 2,
    OpenCvTemplateMatch = 3,
    YoloOnnx = 4,
    TextRegion = 5
}
