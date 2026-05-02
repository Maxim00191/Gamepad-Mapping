#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationTextDetectionOptions(
    int MinimumRegionAreaPx,
    int MorphologyWidth,
    int MorphologyHeight,
    string TargetText = "")
{
    public static AutomationTextDetectionOptions Default { get; } = new(
        24,
        9,
        3,
        "");
}
