#nullable enable

using System.Linq;
using System.Windows;
using System.Windows.Media;
using Gamepad_Mapping.Models.Core.Visual;

namespace Gamepad_Mapping.Utils.ControllerVisual;

public static class ControllerMappingOverlayLeaderGeometry
{
    public const double LeaderPadding = 0d;
    public const double LabelPadding = 0d;
    public const double AnchorHandleRadius = 1.5d;

    public static Point GetConnectionPointOnLabel(Rect labelWorld, ControllerLabelQuadrant quadrant)
    {
        var midY = labelWorld.Y + labelWorld.Height * 0.5;
        return quadrant switch
        {
            ControllerLabelQuadrant.TopLeft or ControllerLabelQuadrant.BottomLeft =>
                new Point(labelWorld.Right, midY),
            ControllerLabelQuadrant.TopRight or ControllerLabelQuadrant.BottomRight =>
                new Point(labelWorld.Left, midY),
            _ => new Point(labelWorld.Left + labelWorld.Width * 0.5, labelWorld.Top + labelWorld.Height * 0.5)
        };
    }

    /// <summary>
    /// Orthogonal polyline (legacy tests / reference). Prefer <see cref="BuildAnchorRelativeBezierLeader"/>.
    /// </summary>
    public static Point[] BuildLeaderPolylineWorldPoints(
        Point anchorWorld,
        Rect labelWorld,
        ControllerLabelQuadrant quadrant,
        double bodyLeft,
        double bodyRight,
        int laneIndex = 0)
    {
        var connection = GetConnectionPointOnLabel(labelWorld, quadrant);
        var isLeftWing = quadrant == ControllerLabelQuadrant.TopLeft || quadrant == ControllerLabelQuadrant.BottomLeft;

        const double minStub = 8.0;
        var corridorPad = 6.0 + laneIndex * 5.5;

        double shoulderX;
        if (isLeftWing)
        {
            var corridorX = bodyLeft - corridorPad;
            shoulderX = Math.Min(anchorWorld.X - minStub, corridorX);
            shoulderX = Math.Min(shoulderX, connection.X - 1d);
            shoulderX = Math.Min(shoulderX, corridorX);
        }
        else
        {
            var corridorX = bodyRight + corridorPad;
            shoulderX = Math.Max(anchorWorld.X + minStub, corridorX);
            shoulderX = Math.Max(shoulderX, connection.X + 1d);
            shoulderX = Math.Max(shoulderX, corridorX);
        }

        var elbow1 = new Point(shoulderX, anchorWorld.Y);
        var elbow2 = new Point(shoulderX, connection.Y);

        return [anchorWorld, elbow1, elbow2, connection];
    }

    public static PathGeometry BuildAnchorRelativeBezierLeader(
        Point anchorWorld,
        Rect labelWorld,
        ControllerLabelQuadrant quadrant,
        double bodyLeft,
        double bodyRight,
        int laneIndex)
    {
        var world = BuildLeaderBezierControlPointsWorld(
            anchorWorld, labelWorld, quadrant, bodyLeft, bodyRight, laneIndex);
        var rel = ToAnchorRelative(anchorWorld, world);
        if (rel.Length != 4)
            return new PathGeometry();

        var fig = new PathFigure { StartPoint = rel[0], IsClosed = false };
        fig.Segments.Add(new BezierSegment(rel[1], rel[2], rel[3], true));
        var g = new PathGeometry([fig]);
        g.Freeze();
        return g;
    }

    private static Point[] BuildLeaderBezierControlPointsWorld(
        Point anchorWorld,
        Rect labelWorld,
        ControllerLabelQuadrant quadrant,
        double bodyLeft,
        double bodyRight,
        int laneIndex)
    {
        var ortho = BuildLeaderPolylineWorldPoints(anchorWorld, labelWorld, quadrant, bodyLeft, bodyRight, laneIndex);
        if (ortho.Length != 4)
            return ortho;

        var elbow1 = ortho[1];
        var elbow2 = ortho[2];
        var end = ortho[3];
        var cp1 = new Point(
            anchorWorld.X + (elbow1.X - anchorWorld.X) * 0.92,
            anchorWorld.Y + (elbow1.Y - anchorWorld.Y) * 0.92);
        var cp2 = new Point(
            end.X + (elbow2.X - end.X) * 0.55,
            end.Y + (elbow2.Y - end.Y) * 0.55);
        return [anchorWorld, cp1, cp2, end];
    }

    public static Point[] ToAnchorRelative(Point anchorWorld, Point[] worldPoints) =>
        worldPoints.Select(p => new Point(p.X - anchorWorld.X, p.Y - anchorWorld.Y)).ToArray();
}
