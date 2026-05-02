#nullable enable

using GamepadMapperGUI.Services.Automation;

namespace GamepadMapping.Tests.Services;

public sealed class AutomationEdgeGeometryBuilderTests
{
    [Fact]
    public void ComputeMinDistanceSquaredToPath_OnHorizontalChord_IsNearZeroAtMidpoint()
    {
        var sut = new AutomationEdgeGeometryBuilder();
        var d = sut.ComputeMinDistanceSquaredToPath(0, 0, 200, 0, 100, 0, samples: 64);
        Assert.True(d < 0.25d, $"expected small distance, got {d}");
    }

    [Fact]
    public void ComputeMinDistanceSquaredToPath_OffCurve_IsPositive()
    {
        var sut = new AutomationEdgeGeometryBuilder();
        var d = sut.ComputeMinDistanceSquaredToPath(0, 0, 200, 0, 100, 80, samples: 64);
        Assert.True(d > 100d, $"expected larger distance, got {d}");
    }
}
