using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationOverlayCoordinateMappingTests
{
    [Fact]
    public void PhysicalRectToOverlay_MapsOriginAndSize()
    {
        var vs = new AutomationVirtualScreenMetrics(0, 0, 1920, 1080);
        var rect = new AutomationPhysicalRect(100, 200, 300, 400);
        AutomationOverlayCoordinateMapping.PhysicalRectToOverlay(rect, vs, 1920, 1080, out var x, out var y, out var w,
            out var h);
        Assert.Equal(100, x, 2);
        Assert.Equal(200, y, 2);
        Assert.Equal(300, w, 2);
        Assert.Equal(400, h, 2);
    }

    [Fact]
    public void PhysicalRectToOverlay_RespectsVirtualScreenOffset()
    {
        var vs = new AutomationVirtualScreenMetrics(-1920, 0, 3840, 1080);
        var rect = new AutomationPhysicalRect(0, 0, 100, 100);
        AutomationOverlayCoordinateMapping.PhysicalRectToOverlay(rect, vs, 1920, 540, out var x, out var y, out _,
            out _);
        Assert.Equal(960, x, 2);
        Assert.Equal(0, y, 2);
    }

    [Fact]
    public void NormalizeVirtualDesktopLogicalBounds_PreservesLeftTop()
    {
        var b = AutomationOverlayCoordinateMapping.NormalizeVirtualDesktopLogicalBounds(-100, 20, 3000, 1500);
        Assert.Equal(-100, b.Left, 2);
        Assert.Equal(20, b.Top, 2);
    }

    [Fact]
    public void NormalizeVirtualDesktopLogicalBounds_ClampsNonPositiveSizeToOne()
    {
        var b = AutomationOverlayCoordinateMapping.NormalizeVirtualDesktopLogicalBounds(0, 0, 0, 0);
        Assert.Equal(1, b.Width, 2);
        Assert.Equal(1, b.Height, 2);
    }

    [Fact]
    public void PhysicalRectToOverlayForClientSurface_OriginIsClientTopLeftOnScreen()
    {
        var rect = new AutomationPhysicalRect(1900, 20, 40, 60);
        AutomationOverlayCoordinateMapping.PhysicalRectToOverlayForClientSurface(rect, 1900, 0, 3840, 2160, 960, 540,
            out var x, out var y, out var w, out var h);
        Assert.Equal(0, x, 2);
        Assert.Equal(5, y, 2);
        Assert.Equal(10, w, 2);
        Assert.Equal(15, h, 2);
    }

    [Fact]
    public void PhysicalRectToOverlayForClientSurface_MapsFullClientToFullOverlay()
    {
        var rect = new AutomationPhysicalRect(-1920, 0, 3840, 2160);
        AutomationOverlayCoordinateMapping.PhysicalRectToOverlayForClientSurface(
            rect,
            -1920,
            0,
            3840,
            2160,
            1536,
            864,
            out var x,
            out var y,
            out var w,
            out var h);
        Assert.Equal(0, x, 2);
        Assert.Equal(0, y, 2);
        Assert.Equal(1536, w, 2);
        Assert.Equal(864, h, 2);
    }

    [Fact]
    public void PhysicalRectToOverlayForClientSurface_UsesPhysicalClientScaleForHighDpiOverlay()
    {
        var rect = new AutomationPhysicalRect(960, 540, 480, 270);
        AutomationOverlayCoordinateMapping.PhysicalRectToOverlayForClientSurface(
            rect,
            0,
            0,
            1920,
            1080,
            1280,
            720,
            out var x,
            out var y,
            out var w,
            out var h);
        Assert.Equal(640, x, 2);
        Assert.Equal(360, y, 2);
        Assert.Equal(320, w, 2);
        Assert.Equal(180, h, 2);
    }
}
