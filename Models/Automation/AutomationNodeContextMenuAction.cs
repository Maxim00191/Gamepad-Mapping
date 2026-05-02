namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeContextMenuAction
{
    public required AutomationNodeContextMenuActionKind Kind { get; init; }

    public required string LabelResourceKey { get; init; }

    public bool IsEnabled { get; init; } = true;
}
