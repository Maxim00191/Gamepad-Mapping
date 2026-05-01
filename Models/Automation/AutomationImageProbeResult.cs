namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationImageProbeResult(
    bool Matched,
    int MatchScreenXPx,
    int MatchScreenYPx,
    int MatchCount = 0,
    double Confidence = 0,
    double BestTemplateCorrelation = 0,
    int MatchWidthPx = 0,
    int MatchHeightPx = 0);
