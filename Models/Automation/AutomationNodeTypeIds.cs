#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationNodeTypeIds
{
    public const string CaptureScreen = "perception.capture_screen";

    public const string BranchCompare = "logic.branch_compare";

    public const string MathClamp = "math.clamp";

    public const string MathDeadband = "math.deadband";

    public const string SignalSmooth = "signal.smooth";
}
