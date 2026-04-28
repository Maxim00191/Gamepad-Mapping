#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationColorDetectionOptions(
    int HueMin,
    int HueMax,
    int SaturationMin,
    int SaturationMax,
    int ValueMin,
    int ValueMax,
    int MinimumAreaPx)
{
    public static AutomationColorDetectionOptions Default { get; } = new(
        0,
        179,
        50,
        255,
        50,
        255,
        8);
}
