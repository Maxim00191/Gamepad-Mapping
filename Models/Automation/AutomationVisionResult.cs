#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public sealed record AutomationVisionResult(
    bool Matched,
    int MatchX,
    int MatchY,
    int MatchCount = 0,
    double Confidence = 0d,
    int BoundingLeft = 0,
    int BoundingTop = 0,
    int BoundingWidth = 0,
    int BoundingHeight = 0);
