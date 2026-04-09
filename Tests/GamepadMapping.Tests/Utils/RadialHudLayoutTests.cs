using System;
using System.Windows;
using System.Windows.Media;
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
    public void Inner_hole_outer_edge_is_slightly_outside_item_ring_inner_edge_by_design()
    {
        var itemRingInnerEdge = RadialHudLayout.ItemCenterRadius - RadialHudLayout.ItemHalf;
        var delta = RadialHudLayout.InnerHoleRadius - itemRingInnerEdge;
        // BaseInnerHoleDiameter 210: inner radius 105·s vs ring inner edge (200−96)·s = 104·s → larger hollow by 1·s.
        Assert.Equal(RadialHudLayout.HudScale, delta, 5);
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
            Assert.Equal(210 * 2.0, RadialHudLayout.InnerHoleDiameter);
            Assert.Equal(112 * 2.0, RadialHudLayout.TitlePlateDiameter);
            Assert.Equal(RadialHudLayout.DiscRadius - RadialHudLayout.ItemHalf, RadialHudLayout.ItemCenterRadius);
        }
        finally
        {
            RadialHudLayout.HudScale = prev;
        }
    }

    [Fact]
    public void Annulus_sector_geometry_empty_when_no_segments()
    {
        var g = RadialHudLayout.CreateAnnulusSectorGeometry(0, 0);
        Assert.True(g.IsEmpty());
    }

    [Fact]
    public void Annulus_sector_geometry_is_full_ring_when_single_segment()
    {
        var g = RadialHudLayout.CreateAnnulusSectorGeometry(0, 1);
        Assert.False(g.IsEmpty());
        Assert.IsType<PathGeometry>(g);
    }

    [Fact]
    public void Annulus_sector_geometry_is_wedge_when_multiple_segments()
    {
        var g = RadialHudLayout.CreateAnnulusSectorGeometry(0, 8);
        Assert.False(g.IsEmpty());
        Assert.IsType<PathGeometry>(g);
    }

    [Fact]
    public void Annulus_sector_top_wedge_contains_outer_midpoint_with_gaps()
    {
        const int n = 4;
        var g = RadialHudLayout.CreateAnnulusSectorGeometry(0, n);
        var cx = RadialHudLayout.DiscRadius;
        var cy = RadialHudLayout.DiscRadius;
        var ro = RadialHudLayout.DiscRadius;
        var midAngle = -Math.PI * 0.5;
        var p = new Point(cx + ro * Math.Cos(midAngle), cy + ro * Math.Sin(midAngle));
        Assert.True(g.FillContains(p, 1.0, ToleranceType.Absolute));
    }

    [Fact]
    public void Sector_gap_arc_length_at_outer_is_near_target()
    {
        var prev = RadialHudLayout.HudScale;
        try
        {
            RadialHudLayout.HudScale = 1.0;
            const int n = 8;
            var r = RadialHudLayout.DiscRadius;
            var step = 2 * Math.PI / n;
            var gapRad = Math.Min(RadialHudLayout.SectorGapLength / r, step * 0.45);
            var arcLen = gapRad * r;
            Assert.InRange(arcLen, 5.0, 7.0);
        }
        finally
        {
            RadialHudLayout.HudScale = prev;
        }
    }
}
