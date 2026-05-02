#nullable enable

namespace GamepadMapperGUI.Interfaces.Services.Automation;

public interface IAutomationEdgeGeometryBuilder
{
    string BuildPathData(double fromX, double fromY, double toX, double toY);

    double ComputeMinDistanceSquaredToPath(double fromX, double fromY, double toX, double toY, double px, double py, int samples = 72);
}
