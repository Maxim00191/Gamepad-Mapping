#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationNodeTypeIds
{
    public const string CaptureScreen = "perception.capture_screen";

    public const string Delay = "automation.delay";

    public const string DebugLog = "debug.log";

    public const string VariablesSet = "variables.set";

    public const string EventEmit = "event.emit";

    public const string KeyboardKey = "output.keyboard_key";

    public const string MouseClick = "output.mouse_click";

    public const string HumanNoise = "output.human_noise";

    public const string LoopJump = "logic.loop_jump";

    public const string BranchBool = "logic.branch_bool";

    public const string BranchCompare = "logic.branch_compare";

    public const string MathClamp = "math.clamp";

    public const string MathDeadband = "math.deadband";

    public const string SignalSmooth = "signal.smooth";
}
