#nullable enable

using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationPortLabelService : IAutomationPortLabelService
{
    public string ResolveDisplayNameResourceKey(string portId, bool isOutputPort, AutomationPortFlowKind flowKind) =>
        portId switch
        {
            "flow.in" => "AutomationPortLabel_FlowIn",
            "flow.out" => "AutomationPortLabel_FlowOut",
            "loop.body" => "AutomationPortLabel_LoopBody",
            "screen.image" => "AutomationPortLabel_ScreenImage",
            "haystack.image" => "AutomationPortLabel_HaystackImage",
            "probe.image" => "AutomationPortLabel_ProbeImage",
            "result.found" => "AutomationPortLabel_ResultFound",
            "result.x" => "AutomationPortLabel_ResultX",
            "result.y" => "AutomationPortLabel_ResultY",
            "result.count" => "AutomationPortLabel_ResultCount",
            "result.pressed" => "AutomationPortLabel_ResultPressed",
            "coord.x" => "AutomationPortLabel_CoordX",
            "coord.y" => "AutomationPortLabel_CoordY",
            "branch.match" => "AutomationPortLabel_BranchMatch",
            "branch.miss" => "AutomationPortLabel_BranchMiss",
            "branch.true" => "AutomationPortLabel_BranchTrue",
            "branch.false" => "AutomationPortLabel_BranchFalse",
            "left" => "AutomationPortLabel_Left",
            "right" => "AutomationPortLabel_Right",
            "value" => "AutomationPortLabel_Value",
            "value.number" => "AutomationPortLabel_ValueNumber",
            "value.bool" => "AutomationPortLabel_ValueBool",
            "value.string" => "AutomationPortLabel_ValueString",
            "condition" => "AutomationPortLabel_Condition",
            "input" => "AutomationPortLabel_Input",
            "case.match" => "AutomationPortLabel_CaseMatch",
            "case.default" => "AutomationPortLabel_CaseDefault",
            "message" => "AutomationPortLabel_Message",
            "current.value" => "AutomationPortLabel_CurrentValue",
            "target.value" => "AutomationPortLabel_TargetValue",
            "control.signal" => "AutomationPortLabel_ControlSignal",
            _ => flowKind == AutomationPortFlowKind.Execution
                ? (isOutputPort ? "AutomationPortLabel_Output" : "AutomationPortLabel_Input")
                : (isOutputPort ? "AutomationPortLabel_DataOutput" : "AutomationPortLabel_DataInput")
        };
}
