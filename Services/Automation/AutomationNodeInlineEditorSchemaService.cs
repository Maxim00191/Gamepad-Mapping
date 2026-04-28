#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationNodeInlineEditorSchemaService : IAutomationNodeInlineEditorSchemaService
{
    private readonly IReadOnlyList<AutomationNodeInlineEditorDefinition> _commonDefinitions = BuildCommonDefinitions();

    private readonly Dictionary<string, IReadOnlyList<AutomationNodeInlineEditorDefinition>> _definitionsByNodeTypeId =
        BuildDefinitions();

    private static readonly IReadOnlyList<string> TemplateNeedleAlgorithmValues =
    [
        AutomationVisionAlgorithmStorage.TemplateMatch,
        AutomationVisionAlgorithmStorage.OpenCvTemplateMatch
    ];

    private static readonly IReadOnlyList<string> YoloAlgorithmValues =
    [
        "",
        AutomationVisionAlgorithmStorage.YoloOnnx
    ];

    private static readonly IReadOnlyList<string> ScoreThresholdAlgorithmValues =
    [
        "",
        AutomationVisionAlgorithmStorage.YoloOnnx,
        AutomationVisionAlgorithmStorage.TemplateMatch,
        AutomationVisionAlgorithmStorage.OpenCvTemplateMatch
    ];

    private static readonly IReadOnlyList<string> ColorAlgorithmValues =
    [
        AutomationVisionAlgorithmStorage.ColorThreshold,
        AutomationVisionAlgorithmStorage.Contour
    ];

    private static readonly IReadOnlyList<string> TextAlgorithmValues =
    [
        AutomationVisionAlgorithmStorage.TextRegion
    ];

    private static readonly IReadOnlyList<string> CaptureSourceProcessWindowValues =
    [
        AutomationCaptureSourceMode.ProcessWindow
    ];

    public IReadOnlyList<AutomationNodeInlineEditorDefinition> GetDefinitions(string nodeTypeId)
    {
        if (!_definitionsByNodeTypeId.TryGetValue(nodeTypeId, out var definitions))
            return _commonDefinitions;

        var combined = new List<AutomationNodeInlineEditorDefinition>(_commonDefinitions.Count + definitions.Count);
        combined.AddRange(_commonDefinitions);
        combined.AddRange(definitions);
        return combined;
    }

    private static IReadOnlyList<AutomationNodeInlineEditorDefinition> BuildCommonDefinitions() =>
    [
        new AutomationNodeInlineEditorDefinition
        {
            NodeTypeId = "*",
            PropertyKey = AutomationNodePropertyKeys.Description,
            LabelResourceKey = "AutomationInlineEditor_NodeDescription",
            PlaceholderResourceKey = "AutomationInlineEditor_NodeDescriptionPlaceholder",
            Kind = AutomationNodeInlineEditorKind.MultilineText,
            DefaultTextValue = ""
        }
    ];

    private static Dictionary<string, IReadOnlyList<AutomationNodeInlineEditorDefinition>> BuildDefinitions() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["perception.capture_screen"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = AutomationNodePropertyKeys.CaptureSourceMode,
                    LabelResourceKey = "AutomationInlineEditor_CaptureSourceMode",
                    Kind = AutomationNodeInlineEditorKind.Choice,
                    DefaultTextValue = AutomationCaptureSourceMode.Screen,
                    ChoiceOptions =
                    [
                        new AutomationNodeInlineChoiceOption
                        {
                            StoredValue = AutomationCaptureSourceMode.Screen,
                            LabelResourceKey = "AutomationInlineEditor_CaptureSourceScreen"
                        },
                        new AutomationNodeInlineChoiceOption
                        {
                            StoredValue = AutomationCaptureSourceMode.ProcessWindow,
                            LabelResourceKey = "AutomationInlineEditor_CaptureSourceProcessWindow"
                        }
                    ]
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = AutomationNodePropertyKeys.CaptureProcessName,
                    LabelResourceKey = "AutomationInlineEditor_CaptureProcessName",
                    PlaceholderResourceKey = "AutomationInlineEditor_CaptureProcessNamePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "",
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.CaptureSourceMode,
                    VisibleWhenPropertyValues = CaptureSourceProcessWindowValues
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.capture_screen",
                    PropertyKey = AutomationNodePropertyKeys.CaptureMode,
                    LabelResourceKey = "AutomationInlineEditor_CaptureMode",
                    Kind = AutomationNodeInlineEditorKind.Choice,
                    DefaultTextValue = "full",
                    ChoiceOptions =
                    [
                        new AutomationNodeInlineChoiceOption
                        {
                            StoredValue = "full",
                            LabelResourceKey = "AutomationInlineEditor_CaptureModeFull"
                        },
                        new AutomationNodeInlineChoiceOption
                        {
                            StoredValue = "roi",
                            LabelResourceKey = "AutomationInlineEditor_CaptureModeRoi"
                        }
                    ]
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
                    ActionLabelResourceKey = "AutomationWorkspace_Browse",
                    SecondaryActionKind = AutomationNodeInlineEditorActionKind.CaptureNeedleImageFromScreen,
                    SecondaryActionLabelResourceKey = "AutomationWorkspace_ScreenshotNeedle",
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = TemplateNeedleAlgorithmValues
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    LabelResourceKey = "AutomationInlineEditor_FindImageAlgorithm",
                    Kind = AutomationNodeInlineEditorKind.Choice,
                    DefaultTextValue = AutomationVisionAlgorithmStorage.YoloOnnx,
                    ChoiceOptions = AutomationVisionAlgorithmCatalog.FindImageAlgorithmChoiceOptions()
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageYoloOnnxPath,
                    LabelResourceKey = "AutomationInlineEditor_FindImageYoloOnnxPath",
                    PlaceholderResourceKey = "AutomationInlineEditor_FindImageYoloOnnxPathPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = AutomationYoloOnnxPaths.DefaultBundledModelRelativePath,
                    ActionKind = AutomationNodeInlineEditorActionKind.BrowseOnnxModelFile,
                    ActionLabelResourceKey = "AutomationWorkspace_Browse",
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = YoloAlgorithmValues
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageYoloClassId,
                    LabelResourceKey = "AutomationInlineEditor_FindImageYoloClassId",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "-1",
                    MinIntegerValue = -1,
                    MaxIntegerValue = 999,
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = YoloAlgorithmValues
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageColorTargetHex,
                    LabelResourceKey = "AutomationInlineEditor_FindImageColorTargetHex",
                    PlaceholderResourceKey = "AutomationInlineEditor_FindImageColorTargetHexPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "",
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = ColorAlgorithmValues
                },
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorHueMin,
                    "AutomationInlineEditor_FindImageColorHueMin",
                    AutomationColorDetectionOptions.Default.HueMin,
                    0,
                    179,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorHueMax,
                    "AutomationInlineEditor_FindImageColorHueMax",
                    AutomationColorDetectionOptions.Default.HueMax,
                    0,
                    179,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorSaturationMin,
                    "AutomationInlineEditor_FindImageColorSaturationMin",
                    AutomationColorDetectionOptions.Default.SaturationMin,
                    0,
                    255,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorSaturationMax,
                    "AutomationInlineEditor_FindImageColorSaturationMax",
                    AutomationColorDetectionOptions.Default.SaturationMax,
                    0,
                    255,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorValueMin,
                    "AutomationInlineEditor_FindImageColorValueMin",
                    AutomationColorDetectionOptions.Default.ValueMin,
                    0,
                    255,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorValueMax,
                    "AutomationInlineEditor_FindImageColorValueMax",
                    AutomationColorDetectionOptions.Default.ValueMax,
                    0,
                    255,
                    ColorAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageColorMinimumAreaPx,
                    "AutomationInlineEditor_FindImageColorMinimumAreaPx",
                    AutomationColorDetectionOptions.Default.MinimumAreaPx,
                    1,
                    1_000_000,
                    ColorAlgorithmValues),
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageTextQuery,
                    LabelResourceKey = "AutomationInlineEditor_FindImageTextQuery",
                    PlaceholderResourceKey = "AutomationInlineEditor_FindImageTextQueryPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "",
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = TextAlgorithmValues
                },
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageTextMinimumRegionAreaPx,
                    "AutomationInlineEditor_FindImageTextMinimumRegionAreaPx",
                    AutomationTextDetectionOptions.Default.MinimumRegionAreaPx,
                    1,
                    1_000_000,
                    TextAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageTextMorphologyWidth,
                    "AutomationInlineEditor_FindImageTextMorphologyWidth",
                    AutomationTextDetectionOptions.Default.MorphologyWidth,
                    1,
                    99,
                    TextAlgorithmValues),
                BuildFindImageIntegerDefinition(
                    AutomationNodePropertyKeys.FindImageTextMorphologyHeight,
                    "AutomationInlineEditor_FindImageTextMorphologyHeight",
                    AutomationTextDetectionOptions.Default.MorphologyHeight,
                    1,
                    99,
                    TextAlgorithmValues),
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageTolerance,
                    LabelResourceKey = "AutomationInlineEditor_FindImageTolerance",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0.15",
                    MinDoubleValue = 0.01,
                    MaxDoubleValue = 0.9,
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = ScoreThresholdAlgorithmValues
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "perception.find_image",
                    PropertyKey = AutomationNodePropertyKeys.FindImageTimeoutMs,
                    LabelResourceKey = "AutomationInlineEditor_FindImageTimeoutMs",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "500",
                    MinIntegerValue = 25,
                    MaxIntegerValue = 30000,
                    VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
                    VisibleWhenPropertyValues = ScoreThresholdAlgorithmValues
                }
            ],
            ["output.keyboard_key"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.keyboard_key",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardActionId,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardActionPickerLabel",
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
                    PropertyKey = AutomationNodePropertyKeys.InputEmulationApiId,
                    LabelResourceKey = "AutomationInlineEditor_InputModePickerLabel",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.PickInputModeId,
                    ActionLabelResourceKey = "AutomationInlineEditor_InputModeButton"
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
                    LabelResourceKey = "AutomationInlineEditor_MouseActionPickerLabel",
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
                    PropertyKey = AutomationNodePropertyKeys.InputEmulationApiId,
                    LabelResourceKey = "AutomationInlineEditor_InputModePickerLabel",
                    Kind = AutomationNodeInlineEditorKind.Action,
                    ActionKind = AutomationNodeInlineEditorActionKind.PickInputModeId,
                    ActionLabelResourceKey = "AutomationInlineEditor_InputModeButton"
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
            ],
            ["control.pid_controller"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "control.pid_controller",
                    PropertyKey = AutomationNodePropertyKeys.PidCurrentValue,
                    LabelResourceKey = "AutomationInlineEditor_PidCurrentValue",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "control.pid_controller",
                    PropertyKey = AutomationNodePropertyKeys.PidTargetValue,
                    LabelResourceKey = "AutomationInlineEditor_PidTargetValue",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "control.pid_controller",
                    PropertyKey = AutomationNodePropertyKeys.PidKp,
                    LabelResourceKey = "AutomationInlineEditor_PidKp",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "1"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "control.pid_controller",
                    PropertyKey = AutomationNodePropertyKeys.PidKi,
                    LabelResourceKey = "AutomationInlineEditor_PidKi",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0"
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "control.pid_controller",
                    PropertyKey = AutomationNodePropertyKeys.PidKd,
                    LabelResourceKey = "AutomationInlineEditor_PidKd",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "0"
                }
            ],
            ["output.key_state"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.key_state",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardKey,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardKey",
                    PlaceholderResourceKey = "AutomationInlineEditor_KeyboardKeyPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.key_state",
                    PropertyKey = AutomationNodePropertyKeys.KeyboardActionMode,
                    LabelResourceKey = "AutomationInlineEditor_KeyboardActionMode",
                    PlaceholderResourceKey = "AutomationInlineEditor_KeyboardActionModePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "hold"
                }
            ],
            ["output.human_noise"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.human_noise",
                    PropertyKey = AutomationNodePropertyKeys.MouseJitterBaseDeltaX,
                    LabelResourceKey = "AutomationInlineEditor_MouseJitterBaseDeltaX",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0",
                    MinIntegerValue = -500,
                    MaxIntegerValue = 500
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.human_noise",
                    PropertyKey = AutomationNodePropertyKeys.MouseJitterBaseDeltaY,
                    LabelResourceKey = "AutomationInlineEditor_MouseJitterBaseDeltaY",
                    Kind = AutomationNodeInlineEditorKind.Integer,
                    DefaultTextValue = "0",
                    MinIntegerValue = -500,
                    MaxIntegerValue = 500
                },
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "output.human_noise",
                    PropertyKey = AutomationNodePropertyKeys.MouseJitterStickMagnitude,
                    LabelResourceKey = "AutomationInlineEditor_MouseJitterStickMagnitude",
                    PlaceholderResourceKey = "AutomationInlineEditor_MouseJitterStickMagnitudePlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Double,
                    DefaultTextValue = "1.0",
                    MinDoubleValue = 0,
                    MaxDoubleValue = 1
                }
            ],
            ["automation.macro"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "automation.macro",
                    PropertyKey = AutomationNodePropertyKeys.MacroSubgraphId,
                    LabelResourceKey = "AutomationInlineEditor_MacroSubgraphId",
                    PlaceholderResourceKey = "AutomationInlineEditor_MacroSubgraphIdPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                }
            ],
            ["event.listener"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "event.listener",
                    PropertyKey = AutomationNodePropertyKeys.EventSignal,
                    LabelResourceKey = "AutomationInlineEditor_EventSignal",
                    PlaceholderResourceKey = "AutomationInlineEditor_EventSignalPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = "engine.start"
                }
            ],
            ["event.emit"] =
            [
                new AutomationNodeInlineEditorDefinition
                {
                    NodeTypeId = "event.emit",
                    PropertyKey = AutomationNodePropertyKeys.EventSignal,
                    LabelResourceKey = "AutomationInlineEditor_EventSignal",
                    PlaceholderResourceKey = "AutomationInlineEditor_EventSignalPlaceholder",
                    Kind = AutomationNodeInlineEditorKind.Text,
                    DefaultTextValue = ""
                }
            ]
        };

    private static AutomationNodeInlineEditorDefinition BuildFindImageIntegerDefinition(
        string propertyKey,
        string labelResourceKey,
        int defaultValue,
        int minValue,
        int maxValue,
        IReadOnlyList<string> visibleWhenValues) =>
        new()
        {
            NodeTypeId = "perception.find_image",
            PropertyKey = propertyKey,
            LabelResourceKey = labelResourceKey,
            Kind = AutomationNodeInlineEditorKind.Integer,
            DefaultTextValue = defaultValue.ToString(),
            MinIntegerValue = minValue,
            MaxIntegerValue = maxValue,
            VisibleWhenPropertyKey = AutomationNodePropertyKeys.FindImageAlgorithm,
            VisibleWhenPropertyValues = visibleWhenValues
        };
}
