#nullable enable

using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

internal static class AutomationNodeInlineEditorSchemaParts
{
    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> MathBinary(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.MathLeft,
            LabelResourceKey = "AutomationInlineEditor_MathLeft",
            Kind = AutomationNodeInlineEditorKind.Double,
            DefaultTextValue = "0"
        },
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.MathRight,
            LabelResourceKey = "AutomationInlineEditor_MathRight",
            Kind = AutomationNodeInlineEditorKind.Double,
            DefaultTextValue = "0"
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> LogicCompare(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.CompareLeft,
            LabelResourceKey = "AutomationInlineEditor_CompareLeft",
            Kind = AutomationNodeInlineEditorKind.Double,
            DefaultTextValue = "0"
        },
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.CompareRight,
            LabelResourceKey = "AutomationInlineEditor_CompareRight",
            Kind = AutomationNodeInlineEditorKind.Double,
            DefaultTextValue = "0"
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> LogicBoolBinary(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.BoolLeft,
            LabelResourceKey = "AutomationInlineEditor_BoolLeft",
            Kind = AutomationNodeInlineEditorKind.Boolean,
            DefaultBooleanValue = false
        },
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.BoolRight,
            LabelResourceKey = "AutomationInlineEditor_BoolRight",
            Kind = AutomationNodeInlineEditorKind.Boolean,
            DefaultBooleanValue = false
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> LogicNot(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.BoolNotInput,
            LabelResourceKey = "AutomationInlineEditor_BoolNotInput",
            Kind = AutomationNodeInlineEditorKind.Boolean,
            DefaultBooleanValue = false
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> RandomInteger(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.RandomMin,
            LabelResourceKey = "AutomationInlineEditor_RandomMin",
            Kind = AutomationNodeInlineEditorKind.Integer,
            DefaultTextValue = "0",
            MinIntegerValue = -1_000_000_000,
            MaxIntegerValue = 1_000_000_000
        },
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.RandomMax,
            LabelResourceKey = "AutomationInlineEditor_RandomMax",
            Kind = AutomationNodeInlineEditorKind.Integer,
            DefaultTextValue = "100",
            MinIntegerValue = -1_000_000_000,
            MaxIntegerValue = 1_000_000_000
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> VariableGet(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.VariableName,
            LabelResourceKey = "AutomationInlineEditor_VariableName",
            PlaceholderResourceKey = "AutomationInlineEditor_VariableNamePlaceholder",
            Kind = AutomationNodeInlineEditorKind.Text,
            DefaultTextValue = ""
        },
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.VariableDefaultValue,
            LabelResourceKey = "AutomationInlineEditor_VariableDefaultValue",
            PlaceholderResourceKey = "AutomationInlineEditor_VariableDefaultValuePlaceholder",
            Kind = AutomationNodeInlineEditorKind.MultilineText,
            DefaultTextValue = ""
        }
    ];

    public static IReadOnlyList<AutomationNodeInlineEditorDefinition> LoopControl(string nodeTypeId) =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = nodeTypeId,
            PropertyKey = AutomationNodePropertyKeys.LoopControlMode,
            LabelResourceKey = "AutomationInlineEditor_LoopControlMode",
            Kind = AutomationNodeInlineEditorKind.Choice,
            DefaultTextValue = "continue",
            ChoiceOptions =
            [
                new AutomationNodeInlineChoiceOption
                {
                    StoredValue = "continue",
                    LabelResourceKey = "AutomationInlineEditor_LoopControlContinue"
                },
                new AutomationNodeInlineChoiceOption
                {
                    StoredValue = "break",
                    LabelResourceKey = "AutomationInlineEditor_LoopControlBreak"
                }
            ]
        }
    ];
}
