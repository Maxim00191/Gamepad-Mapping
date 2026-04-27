namespace GamepadMapperGUI.Models.Automation;

public static class AutomationNodePropertyKeys
{
    public const string CoordinateSpace = "coordinateSpace";

    public const string CaptureMode = "captureMode";

    public const string CaptureRoi = "captureRoi";

    public const string CaptureRoiThumbnailBase64 = "captureRoiThumbnailBase64";

    public const string CaptureRoiCachePath = "captureRoiCachePath";

    public const string KeyboardKey = "keyboardKey";
    public const string KeyboardActionId = "keyboardActionId";
    public const string KeyboardActionMode = "keyboardActionMode";
    public const string KeyboardHoldMilliseconds = "keyboardHoldMs";

    public const string MouseUseMatchPosition = "mouseUseMatchPosition";
    public const string MouseActionId = "mouseActionId";
    public const string MouseActionMode = "mouseActionMode";
    public const string MouseCoordinateMode = "mouseCoordinateMode";
    public const string MouseAbsoluteX = "mouseAbsoluteX";
    public const string MouseAbsoluteY = "mouseAbsoluteY";
    public const string MouseRelativeDeltaX = "mouseRelativeDeltaX";
    public const string MouseRelativeDeltaY = "mouseRelativeDeltaY";
    public const string MouseHumanizeRadiusPx = "mouseHumanizeRadiusPx";
    public const string MouseButton = "mouseButton";

    public const string FindImageTolerance = "findImageTolerance";
    public const string FindImageConfidence = "findImageConfidence";
    public const string FindImageAllowMultipleTargets = "findImageAllowMultipleTargets";

    public const string FindImageTimeoutMs = "findImageTimeoutMs";

    public const string FindImageRoiNodeRef = "findImageRoiNodeRef";

    public const string FindImageNeedlePath = "findImageNeedlePath";

    public const string LoopMaxIterations = "loopMaxIterations";

    public const string DelayMilliseconds = "delayMilliseconds";
    public const string DelayMinMilliseconds = "delayMinMs";
    public const string DelayMaxMilliseconds = "delayMaxMs";

    public const string VariableName = "variableName";
    public const string VariableValue = "variableValue";
    public const string VariableDefaultValue = "variableDefaultValue";

    public const string MathLeft = "mathLeft";
    public const string MathRight = "mathRight";
    public const string BoolLeft = "boolLeft";
    public const string BoolRight = "boolRight";
    public const string CompareLeft = "compareLeft";
    public const string CompareRight = "compareRight";
    public const string SwitchCaseValue = "switchCaseValue";
    public const string LogMessage = "logMessage";
}
