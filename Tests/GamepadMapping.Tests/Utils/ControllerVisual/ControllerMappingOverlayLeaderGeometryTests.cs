using System.Windows;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Utils.ControllerVisual;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class ControllerMappingOverlayLeaderGeometryTests
{
    [Fact]
    public void GetConnectionPointOnLabel_TopLeft_IsBottomRightCorner()
    {
        var r = new Rect(10, 20, 100, 30);
        var p = ControllerMappingOverlayLeaderGeometry.GetConnectionPointOnLabel(r, ControllerLabelQuadrant.TopLeft);
        Assert.Equal(r.Right, p.X);
        Assert.Equal(r.Y + r.Height * 0.5, p.Y);
    }

    [Fact]
    public void BuildLeaderPolylineWorldPoints_IncludesHorizontalElbowAtAnchorY()
    {
        var anchor = new Point(50, 40);
        var label = new Rect(120, 10, 80, 24);
        const double bodyLeft = 44;
        const double bodyRight = 356;
        var pts = ControllerMappingOverlayLeaderGeometry.BuildLeaderPolylineWorldPoints(
            anchor, label, ControllerLabelQuadrant.TopRight, bodyLeft, bodyRight);
        Assert.Equal(4, pts.Length);
        Assert.Equal(anchor, pts[0]);
        Assert.Equal(40, pts[1].Y);
        Assert.Equal(pts[1].X, pts[2].X);
        Assert.Equal(pts[3].Y, pts[2].Y);
    }

    [Fact]
    public void BuildAnchorRelativeBezierLeader_ReturnsNonEmptyFrozenGeometry()
    {
        var anchor = new Point(50, 40);
        var label = new Rect(120, 10, 80, 24);
        const double bodyLeft = 44;
        const double bodyRight = 356;
        var g = ControllerMappingOverlayLeaderGeometry.BuildAnchorRelativeBezierLeader(
            anchor, label, ControllerLabelQuadrant.TopRight, bodyLeft, bodyRight, laneIndex: 1);
        Assert.NotNull(g);
        Assert.True(g.IsFrozen);
        Assert.NotEqual(Rect.Empty, g.Bounds);
    }
}
