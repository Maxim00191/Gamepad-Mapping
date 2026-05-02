#nullable enable

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationVisionTemplateMatchTuning
{
    internal const int CoarseMaxSidePx = 480;
    internal const int MinHaystackPixelsForCoarseFine = 160 * 120;
    internal const int MinNeedleDimensionPx = 10;
    internal const double MinScaleRatioBeforeFullFrameMatch = 0.72;
    internal const double CoarseCorrelationFloorRatio = 0.82;
    internal const double CoarseCorrelationAbsoluteFloor = 0.15;
}
