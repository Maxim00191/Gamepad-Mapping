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
    public void ResolveOverlaps_AdjustsOverlappingLabels()
    {
        // Two labels in the same quadrant (TopLeft) that overlap vertically
        var item1 = new ControllerMappingOverlayItem
        {
            ElementId = "1",
            X = 100, Y = 100,
            LabelX = -160, LabelY = -70, // Result of CalculateLayout for (100,100)
            Quadrant = ControllerLabelQuadrant.TopLeft
        };
        
        var item2 = new ControllerMappingOverlayItem
        {
            ElementId = "2",
            X = 100, Y = 110, // Close to item1
            LabelX = -160, LabelY = -60, // Overlaps with item1
            Quadrant = ControllerLabelQuadrant.TopLeft
        };

        var items = new List<ControllerMappingOverlayItem> { item1, item2 };
        var sizes = new[] { _labelSize, _labelSize };
        _helper.ResolveOverlaps(items, _viewport, sizes);

        // item2 should have been pushed down
        Assert.True(item2.LabelY + item2.Y >= item1.LabelY + item1.Y + _labelSize.Height + 10);
    }

    [Fact]
    public void ResolveOverlaps_KeepsLabelsInViewport()
    {
        // Label pushed near the bottom edge
        var item = new ControllerMappingOverlayItem
        {
            ElementId = "1",
            X = 700, Y = 560,
            LabelX = 60, LabelY = 40, // Abs Y = 600
            Quadrant = ControllerLabelQuadrant.BottomRight
        };

        var items = new List<ControllerMappingOverlayItem> { item };
        _helper.ResolveOverlaps(items, _viewport, [_labelSize]);

        double absY = item.LabelY + item.Y;
        // Viewport bottom is 600, margin is 10, label height is 30
        // Max absY should be 600 - 10 - 30 = 560
        Assert.True(absY <= _viewport.Bottom - 10 - _labelSize.Height + 0.001);
    }
}
