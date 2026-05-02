#nullable enable

using System.Globalization;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationEdgeGeometryBuilder : IAutomationEdgeGeometryBuilder
{
    public string BuildPathData(double fromX, double fromY, double toX, double toY)
    {
        var (c1x, c1y, c2x, c2y) = GetBezierControlPoints(fromX, fromY, toX, toY);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {fromX:0.###},{fromY:0.###} C {c1x:0.###},{c1y:0.###} {c2x:0.###},{c2y:0.###} {toX:0.###},{toY:0.###}");
    }

    public double ComputeMinDistanceSquaredToPath(double fromX, double fromY, double toX, double toY, double px, double py, int samples = 72)
    {
        samples = Math.Clamp(samples, 8, 256);
        var (c1x, c1y, c2x, c2y) = GetBezierControlPoints(fromX, fromY, toX, toY);
        var bestIndex = 0;
        var minSq = double.PositiveInfinity;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var (bx, by) = CubicBezierPoint(fromX, fromY, c1x, c1y, c2x, c2y, toX, toY, t);
            var dx = bx - px;
            var dy = by - py;
            var d = (dx * dx) + (dy * dy);
            if (d < minSq)
            {
                minSq = d;
                bestIndex = i;
            }
        }

        var refineCount = Math.Clamp(AutomationGraphLayoutConstants.EdgeBezierDistanceRefineSamples, 8, 512);
        var lo = bestIndex == 0 ? 0d : (bestIndex - 1) / (double)samples;
        var hi = bestIndex == samples ? 1d : (bestIndex + 1) / (double)samples;
        for (var i = 0; i <= refineCount; i++)
        {
            var t = lo + ((hi - lo) * (i / (double)refineCount));
            var (bx, by) = CubicBezierPoint(fromX, fromY, c1x, c1y, c2x, c2y, toX, toY, t);
            var dx = bx - px;
            var dy = by - py;
            minSq = Math.Min(minSq, (dx * dx) + (dy * dy));
        }

        return minSq;
    }

    private static (double C1x, double C1y, double C2x, double C2y) GetBezierControlPoints(double fromX, double fromY, double toX, double toY)
    {
        var dx = toX - fromX;
        var controlOffset = Math.Clamp(Math.Abs(dx) * 0.45d, 36d, 180d);
        return (fromX + controlOffset, fromY, toX - controlOffset, toY);
    }

    private static (double X, double Y) CubicBezierPoint(
        double p0x,
        double p0y,
        double p1x,
        double p1y,
        double p2x,
        double p2y,
        double p3x,
        double p3y,
        double t)
    {
        var u = 1 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;
        var x = (uuu * p0x) + (3 * uu * t * p1x) + (3 * u * tt * p2x) + (ttt * p3x);
        var y = (uuu * p0y) + (3 * uu * t * p1y) + (3 * u * tt * p2y) + (ttt * p3y);
        return (x, y);
    }
}
