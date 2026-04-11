using System.Collections.Generic;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Services.Input;
using Moq;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HumanizingMouseEmulatorTests
{
    [Fact]
    public void MoveBy_LargeDelta_SubdividesAndSumsToAdjustedDelta()
    {
        var inner = new Mock<IMouseEmulator>();
        var recorded = new List<(int Dx, int Dy)>();
        inner.Setup(m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Callback<int, int, float>((dx, dy, mag) => recorded.Add((dx, dy)));

        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Returns((int x, int y, float mag) => (x, y));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(10, -3);

        Assert.True(recorded.Count >= 2);
        var sx = 0;
        var sy = 0;
        foreach (var (dx, dy) in recorded)
        {
            sx += dx;
            sy += dy;
        }

        Assert.Equal(10, sx);
        Assert.Equal(-3, sy);
    }

    [Fact]
    public void MoveBy_SmallSpan_SingleInnerCall()
    {
        var inner = new Mock<IMouseEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Returns((int x, int y, float mag) => (x, y));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(2, 2);

        inner.Verify(m => m.MoveBy(2, 2, 1.0f), Times.Once);
    }

    [Fact]
    public void MoveBy_ZeroAfterNoise_NoInnerCall()
    {
        var inner = new Mock<IMouseEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(0, 0, It.IsAny<float>())).Returns((0, 0));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(0, 0);

        inner.Verify(m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }
}
