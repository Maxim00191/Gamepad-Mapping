#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public static class AutomationYoloOnnxInferenceDefaults
{
    public const int LetterboxInputSize = 640;

    public const float NmsIoUThreshold = 0.45f;

    public static readonly string[] DefaultModelFileNames = ["yolo.onnx", "model.onnx", "default.onnx"];
}
