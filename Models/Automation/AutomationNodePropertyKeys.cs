namespace GamepadMapperGUI.Models.Automation;

public static class AutomationNodePropertyKeys
{
    public const string Description = "description";

    public const string CoordinateSpace = "coordinateSpace";

    public const string CaptureMode = "captureMode";

    public const string CaptureSourceMode = "captureSourceMode";

    public const string CaptureApi = "captureApi";

    public const string CaptureProcessName = "captureProcessName";

    public const string CaptureCacheRefNodeId = "captureCacheRefNodeId";

    public const string CaptureRoi = "captureRoi";

    public const string CaptureRoiCachePath = "captureRoiCachePath";

    public const string KeyboardKey = "keyboardKey";
    public const string KeyboardActionId = "keyboardActionId";
    public const string KeyboardActionMode = "keyboardActionMode";
    public const string KeyboardHoldMilliseconds = "keyboardHoldMs";
    public const string InputEmulationApiId = "inputEmulationApiId";

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
    public const string MouseJitterBaseDeltaX = "mouseJitterBaseDeltaX";
    public const string MouseJitterBaseDeltaY = "mouseJitterBaseDeltaY";
    public const string MouseJitterStickMagnitude = "mouseJitterStickMagnitude";

    public const string FindImageTolerance = "findImageTolerance";
    public const string FindImageConfidence = "findImageConfidence";
    public const string FindImageAllowMultipleTargets = "findImageAllowMultipleTargets";

    public const string FindImageTimeoutMs = "findImageTimeoutMs";

    public const string FindImageRoiNodeRef = "findImageRoiNodeRef";

    public const string FindImageNeedlePath = "findImageNeedlePath";
    public const string FindImageAlgorithm = "findImageAlgorithm";

    public const string FindImageYoloOnnxPath = "findImageYoloOnnxPath";

    public const string FindImageYoloClassId = "findImageYoloClassId";

    public const string FindImageColorHueMin = "findImageColorHueMin";
    public const string FindImageColorHueMax = "findImageColorHueMax";
    public const string FindImageColorSaturationMin = "findImageColorSaturationMin";
    public const string FindImageColorSaturationMax = "findImageColorSaturationMax";
    public const string FindImageColorValueMin = "findImageColorValueMin";
    public const string FindImageColorValueMax = "findImageColorValueMax";
    public const string FindImageColorMinimumAreaPx = "findImageColorMinimumAreaPx";
    public const string FindImageColorTargetHex = "findImageColorTargetHex";

    public const string FindImageTextMinimumRegionAreaPx = "findImageTextMinimumRegionAreaPx";
    public const string FindImageTextMorphologyWidth = "findImageTextMorphologyWidth";
    public const string FindImageTextMorphologyHeight = "findImageTextMorphologyHeight";
    public const string FindImageTextQuery = "findImageTextQuery";

    public const string FindImageOcrPhrases = "findImageOcrPhrases";

    public const string FindImageOcrCaseSensitive = "findImageOcrCaseSensitive";

    public const string FindImageOcrMaxLongEdgePx = "findImageOcrMaxLongEdgePx";

    public const string LoopMaxIterations = "loopMaxIterations";

    public const string LoopTargetIterationsPerSecond = "loopTargetIterationsPerSecond";

    public const string LoopInteriorSkipDocumentStepInterval = "loopInteriorSkipDocumentStepInterval";

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

    public const string RandomMin = "randomMin";
    public const string RandomMax = "randomMax";

    public const string BoolNotInput = "boolInput";

    public const string LoopControlMode = "loopControlMode";

    public const string PidCurrentValue = "pidCurrentValue";
    public const string PidTargetValue = "pidTargetValue";
    public const string PidKp = "pidKp";
    public const string PidKi = "pidKi";
    public const string PidKd = "pidKd";

    public const string MacroSubgraphId = "macroSubgraphId";
    public const string EventSignal = "eventSignal";
    public const string StateMachineId = "stateMachineId";
}
