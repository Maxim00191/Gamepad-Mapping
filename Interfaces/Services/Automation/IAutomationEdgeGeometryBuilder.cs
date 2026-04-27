#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationEdgeGeometryBuilder
{
    string BuildPathData(double fromX, double fromY, double toX, double toY);
}
