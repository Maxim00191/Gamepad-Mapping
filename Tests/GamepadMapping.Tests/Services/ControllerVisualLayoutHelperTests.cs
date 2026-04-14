using System.Windows;
using Gamepad_Mapping.Models.Core.Visual;
using Gamepad_Mapping.Services;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class ControllerVisualLayoutHelperTests
{
    private readonly ControllerVisualLayoutHelper _helper = new();
    private readonly Size _labelSize = new(100, 30);
    private readonly Rect _viewport = new(0, 0, 800, 600);

    private static bool IntersectsWithGap(Rect a, Rect b, double gap) =>
        !(a.Right + gap <= b.Left || b.Right + gap <= a.Left || a.Bottom + gap <= b.Top || b.Bottom + gap <= a.Top);

    [Theory]
    [InlineData(100, 100, ControllerLabelQuadrant.TopLeft)]
    [InlineData(700, 100, ControllerLabelQuadrant.TopRight)]
    [InlineData(100, 500, ControllerLabelQuadrant.BottomLeft)]
    [InlineData(700, 500, ControllerLabelQuadrant.BottomRight)]
    public void CalculateLayout_AssignsCorrectQuadrant(double x, double y, ControllerLabelQuadrant expectedQuadrant)
    {
        var anchor = new Point(x, y);
        var result = _helper.CalculateLayout("test", anchor, _labelSize, _viewport);
        Assert.Equal(expectedQuadrant, result.Quadrant);
    }

    [Fact]
    public void ArrangeOverlayItems_SeparatesSameWingLabelsVertically()
    {
        var a1 = new Point(100, 100);
        var a2 = new Point(100, 110);
        var item1 = new ControllerMappingOverlayItem
        {
            ElementId = "1",
            X = a1.X,
            Y = a1.Y
        };
        var item2 = new ControllerMappingOverlayItem
        {
            ElementId = "2",
            X = a2.X,
            Y = a2.Y
        };

        var items = new List<ControllerMappingOverlayItem> { item1, item2 };
        var sizes = new[] { _labelSize, _labelSize };
        _helper.ArrangeOverlayItems(items, sizes, _viewport);

        var ra = new Rect(item1.X + item1.LabelX, item1.Y + item1.LabelY, _labelSize.Width, _labelSize.Height);
        var rb = new Rect(item2.X + item2.LabelX, item2.Y + item2.LabelY, _labelSize.Width, _labelSize.Height);
        Assert.False(IntersectsWithGap(ra, rb, 10));
    }

    [Fact]
    public void ArrangeOverlayItems_KeepsLabelsInViewport()
    {
        var item = new ControllerMappingOverlayItem
        {
            ElementId = "1",
            X = 700,
            Y = 560
        };

        var items = new List<ControllerMappingOverlayItem> { item };
        _helper.ArrangeOverlayItems(items, [_labelSize], _viewport);

        double absY = item.LabelY + item.Y;
        const double margin = 12d;
        Assert.True(absY <= _viewport.Bottom - margin - _labelSize.Height + 0.001);
    }
}
