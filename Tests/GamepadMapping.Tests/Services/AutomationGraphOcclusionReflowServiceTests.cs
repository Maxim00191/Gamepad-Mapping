#nullable enable

using GamepadMapperGUI.Models.Automation;
using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationGraphOcclusionReflowServiceTests
{
    private readonly AutomationGraphOcclusionReflowService _sut = new();

    [Fact]
    public void TryComputeRightShift_WhenDownstreamHasEnoughSpace_DoesNotShift()
    {
        var inserted = Bounds(x: 100, y: 100, width: 280, height: 180);
        var downstream = Bounds(x: 420, y: 100, width: 280, height: 180);

        var result = _sut.TryComputeRightShift(inserted, [downstream], gutterLogical: 32, out var shift);

        Assert.False(result);
        Assert.Equal(0d, shift);
    }

    [Fact]
    public void TryComputeRightShift_WhenDownstreamOccludesInsertedNode_ReturnsRequiredShift()
    {
        var inserted = Bounds(x: 100, y: 100, width: 280, height: 180);
        var downstream = Bounds(x: 360, y: 100, width: 280, height: 180);

        var result = _sut.TryComputeRightShift(inserted, [downstream], gutterLogical: 32, out var shift);

        Assert.True(result);
        Assert.Equal(52d, shift);
    }

    [Fact]
    public void TryComputeRightShift_WhenDownstreamIsOnDifferentRow_DoesNotShift()
    {
        var inserted = Bounds(x: 100, y: 100, width: 280, height: 180);
        var downstream = Bounds(x: 240, y: 320, width: 280, height: 180);

        var result = _sut.TryComputeRightShift(inserted, [downstream], gutterLogical: 32, out var shift);

        Assert.False(result);
        Assert.Equal(0d, shift);
    }

    [Fact]
    public void TryComputeRightShift_UsesLargestRequiredShiftAcrossDownstreamNodes()
    {
        var inserted = Bounds(x: 100, y: 100, width: 280, height: 180);
        var first = Bounds(x: 370, y: 100, width: 280, height: 180);
        var second = Bounds(x: 330, y: 120, width: 280, height: 180);

        var result = _sut.TryComputeRightShift(inserted, [first, second], gutterLogical: 32, out var shift);

        Assert.True(result);
        Assert.Equal(82d, shift);
    }

    private static AutomationGraphNodeLayoutBounds Bounds(
        double x,
        double y,
        double width,
        double height) =>
        new(Guid.NewGuid(), x, y, width, height);
}
