using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HardwareStyledMouseSubMoveStepDelayerTests
{
    [Fact]
    public void ComputeNextGapDelay_ExtremeNoiseMultiplier_SumNeverExceedsBatchBudget()
    {
        var noise = new Mock<IHumanInputNoiseController>();
        noise.Setup(n => n.AdjustDelayMs(It.IsAny<int>())).Returns((int baseMs) => baseMs * 50);

        int budgetMs = MouseLookMotionConstraints.GetSubMoveScheduleBudgetMs(10);
        int remaining = budgetMs;
        int gapsRemaining = 11;
        int totalScheduledMs = 0;
        for (int i = 0; i < 11; i++)
            totalScheduledMs += HardwareStyledMouseSubMoveStepDelayer.ComputeNextGapDelayMs(ref remaining, ref gapsRemaining, noise.Object);

        Assert.Equal(0, remaining);
        Assert.Equal(0, gapsRemaining);
        Assert.Equal(budgetMs, totalScheduledMs);
    }

    [Fact]
    public async Task ScheduleSession_SingleStep_NoDelays()
    {
        var noise = new Mock<IHumanInputNoiseController>();
        var delayer = new HardwareStyledMouseSubMoveStepDelayer(noise.Object, () => 10);
        var session = delayer.BeginScheduleSession(1, cancellationToken: default);

        await session.DelayBeforeNextSubMoveAsync();

        noise.Verify(n => n.AdjustDelayMs(It.IsAny<int>()), Times.Never);
    }
}
