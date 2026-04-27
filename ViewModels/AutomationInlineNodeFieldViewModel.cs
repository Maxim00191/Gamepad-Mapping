#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using GamepadMapperGUI.Models.Automation;

namespace Gamepad_Mapping.ViewModels;

public sealed partial class AutomationInlineNodeFieldViewModel : ObservableObject
{
    public required Guid NodeId { get; init; }

    public required string NodeTypeId { get; init; }

    public required string PropertyKey { get; init; }

    public required string Label { get; init; }

    public required AutomationNodeInlineEditorKind Kind { get; init; }

    public string Placeholder { get; init; } = "";

    public AutomationNodeInlineEditorActionKind ActionKind { get; init; } = AutomationNodeInlineEditorActionKind.None;

    public string ActionLabel { get; init; } = "";

    public bool HasAction => ActionKind != AutomationNodeInlineEditorActionKind.None;

    public bool IsBooleanField => Kind == AutomationNodeInlineEditorKind.Boolean;

    public bool IsActionField => Kind == AutomationNodeInlineEditorKind.Action;

    public bool IsTextField => Kind != AutomationNodeInlineEditorKind.Boolean && Kind != AutomationNodeInlineEditorKind.Action;

    [ObservableProperty]
    private string _textValue = "";

    [ObservableProperty]
    private bool _booleanValue;
}
