#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeInlineEditorSchemaService : IAutomationNodeInlineEditorSchemaService
{
    private readonly Dictionary<string, IReadOnlyList<AutomationNodeInlineEditorDefinition>> _definitionsByNodeTypeId =
        BuildDefinitions();

    public IReadOnlyList<AutomationNodeInlineEditorDefinition> GetDefinitions(string nodeTypeId) =>
        _definitionsByNodeTypeId.TryGetValue(nodeTypeId, out var definitions)
            ? definitions
            : [];

    private static Dictionary<string, IReadOnlyList<AutomationNodeInlineEditorDefinition>> BuildDefinitions() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["perception.capture_screen"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = AutomationNodePropertyKeys.CaptureMode,
                    LabelResourceKey = "AutomationInlineEditor_CaptureUseRoi",
                    Kind = AutomationNodeInlineEditorKind.Boolean,
                    DefaultBooleanValue = false
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = "_action.capture.pick_roi",
                    LabelResourceKey = "AutomationInlineEditor_CaptureActions",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.PickCaptureRegion,
                    ActionLabelResourceKey = "AutomationWorkspace_PickRoi"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = "_action.capture.clear_roi",
                    LabelResourceKey = "AutomationInlineEditor_CaptureActions",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.ClearCaptureRegion,
                    ActionLabelResourceKey = "AutomationWorkspace_ClearRoi"
                }
            ],
            ["perception.find_image"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageNeedlePath,
                    LabelResourceKey = "AutomationInlineEditor_FindImageNeedlePath",
                    PlaceholderResourceKey = "AutomationInlineEditor_FindImageNeedlePathPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "",
                    ActionKind = AutomationNodeInlineEditorActionKind.BrowseImageFile,
                    ActionLabelResourceKey = "AutomationWorkspace_Browse"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageTolerance,
                    LabelResourceKey = "AutomationInlineEditor_FindImageTolerance",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0.15",
                    MinDoubleValue = 0.01,
                    MaxDoubleValue = 0.9
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageTimeoutMs,
                    LabelResourceKey = "AutomationInlineEditor_FindImageTimeoutMs",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "500",
                    MinIntegerValue = 25,
                    MaxIntegerValue = 30000
                }
            ],
            ["output.keyboard_key"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.keyboard_key",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardActionId,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardActionId",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.PickKeyboardActionId,
                    ActionLabelResourceKey = "AutomationInlineEditor_KeyboardActionIdButton"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.keyboard_key",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardKey,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardKey",
                    PlaceholderResourceKey = "AutomationInlineEditor_KeyboardKeyPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.keyboard_key",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardActionMode,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardActionMode",
                    PlaceholderResourceKey = "AutomationInlineEditor_KeyboardActionModePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "tap"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.keyboard_key",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardHoldMilliseconds,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardHoldMs",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "200",
                    MinIntegerValue = 1,
                    MaxIntegerValue = 10000
                }
            ],
            ["output.mouse_click"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseActionId,
                    LabelResourceKey = "AutomationInlineEditor_MouseActionId",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.PickMouseActionId,
                    ActionLabelResourceKey = "AutomationInlineEditor_MouseActionIdButton"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseUseMatchPosition,
                    LabelResourceKey = "AutomationInlineEditor_MouseUseMatchPosition",
                    Kind = AutomationNodeInlineEditorKind.Boolean,
                    DefaultBooleanValue = false
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseCoordinateMode,
                    LabelResourceKey = "AutomationInlineEditor_MouseCoordinateMode",
                    PlaceholderResourceKey = "AutomationInlineEditor_MouseCoordinateModePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "dynamic"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseAbsoluteX,
                    LabelResourceKey = "AutomationInlineEditor_MouseAbsoluteX",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseAbsoluteY,
                    LabelResourceKey = "AutomationInlineEditor_MouseAbsoluteY",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseRelativeDeltaX,
                    LabelResourceKey = "AutomationInlineEditor_MouseRelativeX",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseRelativeDeltaY,
                    LabelResourceKey = "AutomationInlineEditor_MouseRelativeY",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseHumanizeRadiusPx,
                    LabelResourceKey = "AutomationInlineEditor_MouseHumanizeRadius",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0",
                    MinIntegerValue = 0,
                    MaxIntegerValue = 25
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseActionMode,
                    LabelResourceKey = "AutomationInlineEditor_MouseActionMode",
                    PlaceholderResourceKey = "AutomationInlineEditor_MouseActionModePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "click"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.MouseButton,
                    LabelResourceKey = "AutomationInlineEditor_MouseButton",
                    PlaceholderResourceKey = "AutomationInlineEditor_MouseButtonPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "left"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.mouse_click",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardHoldMilliseconds,
                    LabelResourceKey = "AutomationInlineEditor_MouseHoldMs",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "120",
                    MinIntegerValue = 1,
                    MaxIntegerValue = 10000
                }
            ],
            ["automation.loop"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "automation.loop",
                    PropertyKey = AutomationNodePropertyKeys.LoopMaxIterations,
                    LabelResourceKey = "AutomationInlineEditor_LoopMaxIterations",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "1",
                    MinIntegerValue = 1,
                    MaxIntegerValue = 1000
                }
            ],
            ["automation.delay"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "automation.delay",
                    PropertyKey = AutomationNodePropertyKeys.DelayMilliseconds,
                    LabelResourceKey = "AutomationInlineEditor_DelayMilliseconds",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "300",
                    MinIntegerValue = 0,
                    MaxIntegerValue = 120000
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "automation.delay",
                    PropertyKey = AutomationNodePropertyKeys.DelayMinMilliseconds,
                    LabelResourceKey = "AutomationInlineEditor_DelayMinMilliseconds",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "300",
                    MinIntegerValue = 0,
                    MaxIntegerValue = 120000
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "automation.delay",
                    PropertyKey = AutomationNodePropertyKeys.DelayMaxMilliseconds,
                    LabelResourceKey = "AutomationInlineEditor_DelayMaxMilliseconds",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "300",
                    MinIntegerValue = 0,
                    MaxIntegerValue = 120000
                }
            ],
            ["variables.set"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "variables.set",
                    PropertyKey = AutomationNodePropertyKeys.VariableName,
                    LabelResourceKey = "AutomationInlineEditor_VariableName",
                    PlaceholderResourceKey = "AutomationInlineEditor_VariableNamePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "variables.set",
                    PropertyKey = AutomationNodePropertyKeys.VariableValue,
                    LabelResourceKey = "AutomationInlineEditor_VariableValue",
                    PlaceholderResourceKey = "AutomationInlineEditor_VariableValuePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                }
            ],
            ["logic.switch"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "logic.switch",
                    PropertyKey = AutomationNodePropertyKeys.SwitchCaseValue,
                    LabelResourceKey = "AutomationInlineEditor_SwitchCaseValue",
                    PlaceholderResourceKey = "AutomationInlineEditor_SwitchCaseValuePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                }
            ],
            ["debug.log"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "debug.log",
                    PropertyKey = AutomationNodePropertyKeys.LogMessage,
                    LabelResourceKey = "AutomationInlineEditor_LogMessage",
                    PlaceholderResourceKey = "AutomationInlineEditor_LogMessagePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                }
            ]
        };
}
