namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationImageProbeOptions(
    double Tolerance01,
    int TimeoutMs,
    string? YoloOnnxModelPath = null,
    int YoloClassIdFilter = -1,
    AutomationColorDetectionOptions ColorDetectionOptions = default,
    AutomationTextDetectionOptions TextDetectionOptions = default,
    AutomationOcrPhraseMatchOptions OcrPhraseMatchOptions = default)
{
    public AutomationColorDetectionOptions EffectiveColorDetectionOptions =>
        ColorDetectionOptions == default
            ? AutomationColorDetectionOptions.Default
            : ColorDetectionOptions;

    public AutomationTextDetectionOptions EffectiveTextDetectionOptions =>
        TextDetectionOptions == default
            ? AutomationTextDetectionOptions.Default
            : TextDetectionOptions;

    public AutomationOcrPhraseMatchOptions EffectiveOcrPhraseMatchOptions =>
        OcrPhraseMatchOptions == default
            ? AutomationOcrPhraseMatchOptions.Default
            : OcrPhraseMatchOptions;
}
