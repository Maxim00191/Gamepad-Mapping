#nullable enable

using System.Globalization;
using GamepadMapperGUI.Interfaces.Services.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationEdgeGeometryBuilder : IAutomationEdgeGeometryBuilder
{
    public string BuildPathData(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var controlOffset = Math.Clamp(Math.Abs(dx) * 0.45d, 36d, 180d);
        var c1x = fromX + controlOffset;
        var c1y = fromY;
        var c2x = toX - controlOffset;
        var c2y = toY;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {fromX:0.###},{fromY:0.###} C {c1x:0.###},{c1y:0.###} {c2x:0.###},{c2y:0.###} {toX:0.###},{toY:0.###}");
    }
}
