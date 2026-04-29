#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class NodeTypeRegistry : INodeTypeRegistry
{
    private readonly Dictionary<string, AutomationNodeTypeDefinition> _byId;

    public NodeTypeRegistry()
    {
        var defs = BuildDefinitions();
        _byId = defs.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<AutomationNodeTypeDefinition> AllDefinitions => _byId.Values;

    public AutomationNodeTypeDefinition GetRequired(string nodeTypeId) =>
        TryGet(nodeTypeId, out var def) && def is not null
            ? def
            : throw new KeyNotFoundException($"Unknown automation node type '{nodeTypeId}'.");

    public bool TryGet(string nodeTypeId, out AutomationNodeTypeDefinition? definition)
    {
        var found = _byId.TryGetValue(nodeTypeId, out var def);
        definition = def;
        return found;
    }

    public AutomationPortDescriptor? ResolveInputPort(string nodeTypeId, string portId)
    {
        if (!TryGet(nodeTypeId, out var def) || def is null)
            return null;

        foreach (var p in def.InputPorts)
        {
            if (string.Equals(p.Id, portId, StringComparison.Ordinal))
                return p;
        }

        return null;
    }

    public AutomationPortDescriptor? ResolveOutputPort(string nodeTypeId, string portId)
    {
        if (!TryGet(nodeTypeId, out var def) || def is null)
            return null;

        foreach (var p in def.OutputPorts)
        {
            if (string.Equals(p.Id, portId, StringComparison.Ordinal))
                return p;
        }

        return null;
    }

    private static IReadOnlyList<AutomationNodeTypeDefinition> BuildDefinitions() =>
    [
        new AutomationNodeTypeDefinition
        {
            Id = "automation.loop",
            DisplayNameResourceKey = "AutomationNode_Display_Loop",
            SummaryResourceKey = "AutomationNode_Summary_Loop",
            GlyphFontGlyph = "\uEF3B",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "loop.body",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "perception.capture_screen",
            DisplayNameResourceKey = "AutomationNode_Display_CaptureScreen",
            SummaryResourceKey = "AutomationNode_Summary_CaptureScreen",
            GlyphFontGlyph = "\uE722",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "screen.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "automation.delay",
            DisplayNameResourceKey = "AutomationNode_Display_Delay",
            SummaryResourceKey = "AutomationNode_Summary_Delay",
            GlyphFontGlyph = "\uE823",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "perception.find_image",
            DisplayNameResourceKey = "AutomationNode_Display_FindImage",
            SummaryResourceKey = "AutomationNode_Summary_FindImage",
            GlyphFontGlyph = "\uE11A",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "haystack.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "probe.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "result.found",
                    PortType = AutomationPortType.Boolean,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "result.x",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "result.y",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "result.count",
                    PortType = AutomationPortType.Integer,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "logic.branch_image",
            DisplayNameResourceKey = "AutomationNode_Display_BranchImage",
            SummaryResourceKey = "AutomationNode_Summary_BranchImage",
            GlyphFontGlyph = "\uE1C3",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "probe.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "coord.x",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "coord.y",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "branch.match",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                },
                new AutomationPortDescriptor
                {
                    Id = "branch.miss",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "output.keyboard_key",
            DisplayNameResourceKey = "AutomationNode_Display_KeyboardKey",
            SummaryResourceKey = "AutomationNode_Summary_KeyboardKey",
            GlyphFontGlyph = "\uE765",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "probe.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = AutomationPortIds.Condition,
                    PortType = AutomationPortType.Boolean,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                }
            ]
        },
        new AutomationNodeTypeDefinition
        {
            Id = "output.mouse_click",
            DisplayNameResourceKey = "AutomationNode_Display_MouseClick",
            SummaryResourceKey = "AutomationNode_Summary_MouseClick",
            GlyphFontGlyph = "\uE962",
            InputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.in",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "probe.image",
                    PortType = AutomationPortType.ImageOrCoordinates,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "coord.x",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = "coord.y",
                    PortType = AutomationPortType.Number,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                },
                new AutomationPortDescriptor
                {
                    Id = AutomationPortIds.Condition,
                    PortType = AutomationPortType.Boolean,
                    FlowKind = AutomationPortFlowKind.Data,
                    IsOutput = false
                }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor
                {
                    Id = "flow.out",
                    PortType = AutomationPortType.Execution,
                    FlowKind = AutomationPortFlowKind.Execution,
                    IsOutput = true
                }
            ]
        },
        BuildBinaryMath("math.add", "AutomationNode_Display_Add", "\uE110"),
        BuildBinaryMath("math.subtract", "AutomationNode_Display_Subtract", "\uE113"),
        BuildBinaryMath("math.multiply", "AutomationNode_Display_Multiply", "\uE118"),
        BuildBinaryMath("math.divide", "AutomationNode_Display_Divide", "\uE11A"),
        BuildClampNode(),
        BuildDeadbandNode(),
        BuildSignalSmoothNode(),
        BuildComparison("logic.gt", "AutomationNode_Display_GreaterThan", "\uE8D1"),
        BuildComparison("logic.lt", "AutomationNode_Display_LessThan", "\uE8D2"),
        BuildComparison("logic.eq", "AutomationNode_Display_Equals", "\uE8D5"),
        BuildBinaryBoolean("logic.and", "AutomationNode_Display_And", "\uE7E8"),
        BuildBinaryBoolean("logic.or", "AutomationNode_Display_Or", "\uE7E7"),
        BuildUnaryBoolean("logic.not", "AutomationNode_Display_Not", "\uE7E6"),
        BuildRandomNode(),
        BuildGetVariableNode(),
        BuildSetVariableNode(),
        BuildBranchBoolNode(),
        BuildBranchCompareNode(),
        BuildSwitchNode(),
        BuildLoopControlNode(),
        BuildPidControllerNode(),
        BuildKeyStateNode(),
        BuildMouseJitterNode(),
        BuildMacroNode(),
        BuildEventListenerNode(),
        BuildEventEmitNode(),
        BuildLogNode()
    ];

    private static AutomationNodeTypeDefinition BuildBinaryMath(string id, string displayKey, string glyph) =>
        new()
        {
            Id = id,
            DisplayNameResourceKey = displayKey,
            SummaryResourceKey = "AutomationNode_Summary_MathBinary",
            GlyphFontGlyph = glyph,
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "left", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "right", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildClampNode() =>
        new()
        {
            Id = AutomationNodeTypeIds.MathClamp,
            DisplayNameResourceKey = "AutomationNode_Display_Clamp",
            SummaryResourceKey = "AutomationNode_Summary_Clamp",
            GlyphFontGlyph = "\uE9D9",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "input", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "min", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "max", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildDeadbandNode() =>
        new()
        {
            Id = AutomationNodeTypeIds.MathDeadband,
            DisplayNameResourceKey = "AutomationNode_Display_Deadband",
            SummaryResourceKey = "AutomationNode_Summary_Deadband",
            GlyphFontGlyph = "\uE9D9",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "input", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "threshold", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildSignalSmoothNode() =>
        new()
        {
            Id = AutomationNodeTypeIds.SignalSmooth,
            DisplayNameResourceKey = "AutomationNode_Display_SignalSmooth",
            SummaryResourceKey = "AutomationNode_Summary_SignalSmooth",
            GlyphFontGlyph = "\uE9D9",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "input", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildComparison(string id, string displayKey, string glyph) =>
        new()
        {
            Id = id,
            DisplayNameResourceKey = displayKey,
            SummaryResourceKey = "AutomationNode_Summary_Comparison",
            GlyphFontGlyph = glyph,
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "left", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "right", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildBinaryBoolean(string id, string displayKey, string glyph) =>
        new()
        {
            Id = id,
            DisplayNameResourceKey = displayKey,
            SummaryResourceKey = "AutomationNode_Summary_BooleanBinary",
            GlyphFontGlyph = glyph,
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "left", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "right", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildUnaryBoolean(string id, string displayKey, string glyph) =>
        new()
        {
            Id = id,
            DisplayNameResourceKey = displayKey,
            SummaryResourceKey = "AutomationNode_Summary_BooleanUnary",
            GlyphFontGlyph = glyph,
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "input", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildRandomNode() =>
        new()
        {
            Id = "math.random",
            DisplayNameResourceKey = "AutomationNode_Display_Random",
            SummaryResourceKey = "AutomationNode_Summary_Random",
            GlyphFontGlyph = "\uF87B",
            InputPorts = [],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Integer, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildGetVariableNode() =>
        new()
        {
            Id = "variables.get",
            DisplayNameResourceKey = "AutomationNode_Display_GetVariable",
            SummaryResourceKey = "AutomationNode_Summary_GetVariable",
            GlyphFontGlyph = "\uECAA",
            InputPorts = [],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.Any, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildSetVariableNode() =>
        new()
        {
            Id = "variables.set",
            DisplayNameResourceKey = "AutomationNode_Display_SetVariable",
            SummaryResourceKey = "AutomationNode_Summary_SetVariable",
            GlyphFontGlyph = "\uE70F",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false },
                new AutomationPortDescriptor { Id = "value.number", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "value.bool", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "value.string", PortType = AutomationPortType.String, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildBranchBoolNode() =>
        new()
        {
            Id = "logic.branch_bool",
            DisplayNameResourceKey = "AutomationNode_Display_BranchBool",
            SummaryResourceKey = "AutomationNode_Summary_BranchBool",
            GlyphFontGlyph = "\uEBE7",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false },
                new AutomationPortDescriptor { Id = "condition", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "branch.true", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true },
                new AutomationPortDescriptor { Id = "branch.false", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildBranchCompareNode() =>
        new()
        {
            Id = AutomationNodeTypeIds.BranchCompare,
            DisplayNameResourceKey = "AutomationNode_Display_BranchCompare",
            SummaryResourceKey = "AutomationNode_Summary_BranchCompare",
            GlyphFontGlyph = "\uEBE7",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false },
                new AutomationPortDescriptor { Id = "left", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "right", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = AutomationPortIds.BranchTrue, PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true },
                new AutomationPortDescriptor { Id = AutomationPortIds.BranchFalse, PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildSwitchNode() =>
        new()
        {
            Id = "logic.switch",
            DisplayNameResourceKey = "AutomationNode_Display_Switch",
            SummaryResourceKey = "AutomationNode_Summary_Switch",
            GlyphFontGlyph = "\uE8EF",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false },
                new AutomationPortDescriptor { Id = "value", PortType = AutomationPortType.String, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "case.match", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true },
                new AutomationPortDescriptor { Id = "case.default", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildLoopControlNode() =>
        new()
        {
            Id = "logic.loop_control",
            DisplayNameResourceKey = "AutomationNode_Display_LoopControl",
            SummaryResourceKey = "AutomationNode_Summary_LoopControl",
            GlyphFontGlyph = "\uE101",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildLogNode() =>
        new()
        {
            Id = "debug.log",
            DisplayNameResourceKey = "AutomationNode_Display_Log",
            SummaryResourceKey = "AutomationNode_Summary_Log",
            GlyphFontGlyph = "\uE8BD",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false },
                new AutomationPortDescriptor { Id = "message", PortType = AutomationPortType.String, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        }
    ;

    private static AutomationNodeTypeDefinition BuildMacroNode() =>
        new()
        {
            Id = "automation.macro",
            DisplayNameResourceKey = "AutomationNode_Display_Macro",
            SummaryResourceKey = "AutomationNode_Summary_Macro",
            GlyphFontGlyph = "\uE943",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildEventListenerNode() =>
        new()
        {
            Id = "event.listener",
            DisplayNameResourceKey = "AutomationNode_Display_EventListener",
            SummaryResourceKey = "AutomationNode_Summary_EventListener",
            GlyphFontGlyph = "\uE7C3",
            InputPorts = [],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildEventEmitNode() =>
        new()
        {
            Id = "event.emit",
            DisplayNameResourceKey = "AutomationNode_Display_EventEmit",
            SummaryResourceKey = "AutomationNode_Summary_EventEmit",
            GlyphFontGlyph = "\uE7C3",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildPidControllerNode() =>
        new()
        {
            Id = "control.pid_controller",
            DisplayNameResourceKey = "AutomationNode_Display_PidController",
            SummaryResourceKey = "AutomationNode_Summary_PidController",
            GlyphFontGlyph = "\uEC8A",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "current.value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false },
                new AutomationPortDescriptor { Id = "target.value", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "control.signal", PortType = AutomationPortType.Number, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildKeyStateNode() =>
        new()
        {
            Id = "output.key_state",
            DisplayNameResourceKey = "AutomationNode_Display_KeyState",
            SummaryResourceKey = "AutomationNode_Summary_KeyState",
            GlyphFontGlyph = "\uE765",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true },
                new AutomationPortDescriptor { Id = "result.pressed", PortType = AutomationPortType.Boolean, FlowKind = AutomationPortFlowKind.Data, IsOutput = true }
            ]
        };

    private static AutomationNodeTypeDefinition BuildMouseJitterNode() =>
        new()
        {
            Id = "output.human_noise",
            DisplayNameResourceKey = "AutomationNode_Display_MouseJitter",
            SummaryResourceKey = "AutomationNode_Summary_MouseJitter",
            GlyphFontGlyph = "\uF8A3",
            InputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.in", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = false }
            ],
            OutputPorts =
            [
                new AutomationPortDescriptor { Id = "flow.out", PortType = AutomationPortType.Execution, FlowKind = AutomationPortFlowKind.Execution, IsOutput = true }
            ]
        };
}
