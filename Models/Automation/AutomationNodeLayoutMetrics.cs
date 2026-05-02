namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeLayoutMetrics
{
    public required double NodeWidth { get; init; }

    public required double VisualMinHeight { get; init; }

    public required double ContentMinWidth { get; init; }

    public required double OutputPortRowMinHeight { get; init; }

    public required double InputPortRowMinHeight { get; init; }

    public required double SettingsSectionMinHeight { get; init; }
}
