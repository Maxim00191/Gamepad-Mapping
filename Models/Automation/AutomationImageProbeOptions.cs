namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationImageProbeOptions(
    double Tolerance01,
    int TimeoutMs,
    string? YoloOnnxModelPath = null,
    int YoloClassIdFilter = -1,
    AutomationColorDetectionOptions ColorDetectionOptions = default,
    AutomationTextDetectionOptions TextDetectionOptions = default,
    AutomationOcrPhraseMatchOptions OcrPhraseMatchOptions = default,
    double TemplateMatchMinNormalizedCorrelation = double.NaN)
{
    public static double CombineTemplateMatchMinNormalizedCorrelation(double tolerance01, double confidence01)
    {
        var tol = Math.Clamp(tolerance01, 0, 0.9);
        var conf = Math.Clamp(confidence01, 0, 1);
        return Math.Max(1.0 - tol, conf);
    }

    public double ResolveTemplateMatchMinNormalizedCorrelation()
    {
        if (!double.IsNaN(TemplateMatchMinNormalizedCorrelation))
            return Math.Clamp(TemplateMatchMinNormalizedCorrelation, 0, 1);

        return 1.0 - Math.Clamp(Tolerance01, 0, 0.9);
    }

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
