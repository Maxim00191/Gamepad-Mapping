using Gamepad_Mapping.Utils;
using Xunit;

namespace GamepadMapping.Tests.Utils;

public class RadialHudLayoutTests
{
    [Fact]
    public void ItemCenterRadius_is_disc_minus_half_slot()
    {
        Assert.Equal(RadialHudLayout.DiscRadius - RadialHudLayout.ItemHalf, RadialHudLayout.ItemCenterRadius);
    }

    [Fact]
    public void Inner_hole_does_not_overlap_item_ring()
    {
        var itemRingInnerEdge = RadialHudLayout.ItemCenterRadius - RadialHudLayout.ItemHalf;
        Assert.True(RadialHudLayout.InnerHoleRadius < itemRingInnerEdge);
    }

    [Fact]
    public void Title_plate_fits_inside_inner_hole_for_hollow_gap()
    {
        Assert.True(RadialHudLayout.TitlePlateRadius < RadialHudLayout.InnerHoleRadius);
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

    [Fact]
    public void HudScale_scales_disc_and_slots_together()
    {
        var prev = RadialHudLayout.HudScale;
        try
        {
            RadialHudLayout.HudScale = 2.0;
            Assert.Equal(400 * 2.0, RadialHudLayout.DiscDiameter);
            Assert.Equal(96 * 2.0, RadialHudLayout.ItemSize);
            Assert.Equal(140 * 2.0, RadialHudLayout.InnerHoleDiameter);
            Assert.Equal(112 * 2.0, RadialHudLayout.TitlePlateDiameter);
            Assert.Equal(RadialHudLayout.DiscRadius - RadialHudLayout.ItemHalf, RadialHudLayout.ItemCenterRadius);
        }
        finally
        {
            RadialHudLayout.HudScale = prev;
        }
    }
}
