namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationTemplateMatchResult(
    bool Matched,
    int MatchX,
    int MatchY,
    double Confidence);
