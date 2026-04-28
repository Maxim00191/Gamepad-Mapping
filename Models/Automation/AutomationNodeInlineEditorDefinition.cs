#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed class AutomationNodeInlineEditorDefinition
{
    public required string NodeTypeId { get; init; }

    public required string PropertyKey { get; init; }

    public required string LabelResourceKey { get; init; }

    public required AutomationNodeInlineEditorKind Kind { get; init; }

    public string? PlaceholderResourceKey { get; init; }

    public AutomationNodeInlineEditorActionKind ActionKind { get; init; } = AutomationNodeInlineEditorActionKind.None;

    public string? ActionLabelResourceKey { get; init; }

    public AutomationNodeInlineEditorActionKind SecondaryActionKind { get; init; } = AutomationNodeInlineEditorActionKind.None;

    public string? SecondaryActionLabelResourceKey { get; init; }

    public string DefaultTextValue { get; init; } = "";

    public bool DefaultBooleanValue { get; init; }

    public int? MinIntegerValue { get; init; }

    public int? MaxIntegerValue { get; init; }

    public double? MinDoubleValue { get; init; }

    public double? MaxDoubleValue { get; init; }

    public IReadOnlyList<AutomationNodeInlineChoiceOption>? ChoiceOptions { get; init; }
}
