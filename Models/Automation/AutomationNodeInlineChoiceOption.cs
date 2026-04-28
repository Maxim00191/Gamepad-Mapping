#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeInlineChoiceOption
{
    public required string StoredValue { get; init; }

    public required string LabelResourceKey { get; init; }
}
