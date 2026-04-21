using System.Collections.Generic;
using System.Linq;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HumanizingMouseEmulatorTests
{
    private static (Mock<IMouseEmulator> Inner, List<(int Dx, int Dy)> Recorded) CreateInnerRecorder()
    {
        var recorded = new List<(int Dx, int Dy)>();
        var inner = new Mock<IMouseEmulator>();
        inner.Setup(m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()))
            .Callback<int, int, float, GamepadBindingType?>((dx, dy, mag, _) => recorded.Add((dx, dy)));
        return (inner, recorded);
    }

    [Fact]
    public void MoveBy_LargeDelta_SubdividesAndSumsToAdjustedDelta()
    {
        var (inner, recorded) = CreateInnerRecorder();

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

        inner.Verify(m => m.MoveBy(2, 2, 1.0f, null), Times.Once());
    }

    [Fact]
    public void MoveBy_ZeroAfterNoise_NoInnerCall()
    {
        var inner = new Mock<IMouseEmulator>();
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(0, 0, It.IsAny<float>())).Returns((0, 0));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(0, 0);

        inner.Verify(m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()), Times.Never());
    }

    [Fact]
    public void MoveBy_LargeDelta_CapsSubMovesPerPoll()
    {
        var (inner, recorded) = CreateInnerRecorder();

        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Returns((int x, int y, float mag) => (x, y));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(80, 0);

        Assert.Equal(MouseLookMotionConstraints.MaxSubMovesPerGamepadPoll, recorded.Count);
        Assert.True(recorded.Sum(t => t.Dx) < 80);
    }

    [Fact]
    public void ClearPendingSubdivision_DropsCarryBeforeNextMechanicalMove()
    {
        var (inner, recorded) = CreateInnerRecorder();

        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Returns((int x, int y, float mag) => (x, y));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(80, 0);
        recorded.Clear();

        ((IPendingMouseSubdivisionState)emu).ClearPendingSubdivision();
        emu.MoveBy(3, 0);

        Assert.Equal(3, recorded.Sum(t => t.Dx));
    }

    [Fact]
    public void ClearPendingSubdivision_LeftOnly_PreservesRightStickCarry()
    {
        var (inner, recorded) = CreateInnerRecorder();

        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustMouseMove(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()))
            .Returns((int x, int y, float mag) => (x, y));

        var emu = new HumanizingMouseEmulator(inner.Object, noise.Object);
        emu.MoveBy(80, 0, 1.0f, GamepadBindingType.RightThumbstick);
        recorded.Clear();

        ((IPendingMouseSubdivisionState)emu).ClearPendingSubdivision(GamepadBindingType.LeftThumbstick);
        emu.MoveBy(3, 0, 1.0f, GamepadBindingType.RightThumbstick);

        // If carry had been cleared, a delta of 3 would stay below subdivision span and emit ~3 px total.
        Assert.True(recorded.Sum(t => t.Dx) > 12, "right-stick carry should survive clearing the left scope only");
    }
}
