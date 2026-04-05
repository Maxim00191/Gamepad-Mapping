using Gamepad_Mapping.Utils;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class RadialHudLayoutTests
{
    [Fact]
    public void ItemCenterRadius_is_disc_minus_half_slot()
    {
        Assert.Equal(170 - 40, RadialHudLayout.ItemCenterRadius);
    }

    [Fact]
    public void ItemSlotTopLeft_first_segment_is_top_center_of_canvas()
    {
        var tl = RadialHudLayout.ItemSlotTopLeft(0, 8);
        var cx = RadialHudLayout.DiscRadius;
        var cy = RadialHudLayout.DiscRadius;
        var r = RadialHudLayout.ItemCenterRadius;
        Assert.Equal(cx - RadialHudLayout.ItemHalf, tl.X, 5);
        Assert.Equal(cy - r - RadialHudLayout.ItemHalf, tl.Y, 5);
    }

    [Fact]
    public void ItemSlotTopLeft_four_items_cardinal_slots()
    {
        var n = 4;
        var r = RadialHudLayout.ItemCenterRadius;
        var h = RadialHudLayout.ItemHalf;
        var c = RadialHudLayout.DiscRadius;

        var top = RadialHudLayout.ItemSlotTopLeft(0, n);
        Assert.Equal(c - h, top.X, 5);
        Assert.Equal(c - r - h, top.Y, 5);

        var right = RadialHudLayout.ItemSlotTopLeft(1, n);
        Assert.Equal(c + r - h, right.X, 5);
        Assert.Equal(c - h, right.Y, 5);

        var bottom = RadialHudLayout.ItemSlotTopLeft(2, n);
        Assert.Equal(c - h, bottom.X, 5);
        Assert.Equal(c + r - h, bottom.Y, 5);

        var left = RadialHudLayout.ItemSlotTopLeft(3, n);
        Assert.Equal(c - r - h, left.X, 5);
        Assert.Equal(c - h, left.Y, 5);
    }
}
