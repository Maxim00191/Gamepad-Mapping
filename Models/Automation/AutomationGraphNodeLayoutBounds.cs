#nullable enable

namespace GamepadMapperGUI.Models.Automation;

public readonly record struct AutomationGraphNodeLayoutBounds(
    Guid NodeId,
    double X,
    double Y,
    double Width,
    double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}
