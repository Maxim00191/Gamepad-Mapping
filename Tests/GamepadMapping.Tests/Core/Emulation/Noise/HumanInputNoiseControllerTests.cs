using GamepadMapperGUI.Core;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HumanInputNoiseControllerTests
{
    private sealed class SteppingTimeProvider : ITimeProvider
    {
        public long Ticks;

        public long GetTickCount64() => Ticks;

        public global::GamepadMapperGUI.Interfaces.Core.ITimer CreateTimer(TimeSpan interval, Action onTick) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void Disabled_PassesDelayThrough()
    {
        var noise = new NoiseGenerator(42);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(false, 1f, 1f, 0f), time);
        Assert.Equal(30, c.AdjustDelayMs(30));
    }

    [Fact]
    public void Enabled_AdjustsDelay()
    {
        var noise = new NoiseGenerator(7);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(true, 0.5f, 0.5f, 0f), time);
        int a = c.AdjustDelayMs(100);
        int b = c.AdjustDelayMs(100);
        Assert.True(a > 0);
        Assert.NotEqual(100, a);
    }

    [Fact]
    public void Disabled_MouseUnchanged()
    {
        var noise = new NoiseGenerator(1);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(false, 1f, 1f, 0f), time);
        Assert.Equal((3, 4), c.AdjustMouseMove(3, 4));
    }

    [Fact]
    public void MouseMove_SubPixelJitterAccumulatesAcrossCalls()
    {
        var noise = new NoiseGenerator(99);
        var time = new SteppingTimeProvider { Ticks = 10_000 };
        var p = new HumanInputNoiseParameters(true, 0.01f, 1f, 0f);
        var c = new HumanInputNoiseController(noise, () => p, time);

        var sumX = 0;
        for (var i = 0; i < 400; i++)
        {
            time.Ticks += 16;
            var (dx, _) = c.AdjustMouseMove(5, 0);
            sumX += dx - 5;
        }

        Assert.NotEqual(0, sumX);
    }

    [Fact]
    public void AmplitudeZero_MouseMove_EqualsOriginalDelta_Strictly()
    {
        var noise = new NoiseGenerator(42);
        var time = new SteppingTimeProvider { Ticks = 20_000 };
        var p = new HumanInputNoiseParameters(true, 0f, 0.5f, 0f);
        var c = new HumanInputNoiseController(noise, () => p, time);

        foreach (var (dx, dy) in new (int, int)[] { (1, 0), (-3, 4), (0, 7), (-12, -12) })
        {
            time.Ticks += 16;
            Assert.Equal((dx, dy), c.AdjustMouseMove(dx, dy));
        }
    }

    [Fact]
    public void AmplitudeZero_ClearsResidual_AfterPriorJitter()
    {
        var noise = new NoiseGenerator(77);
        var time = new SteppingTimeProvider { Ticks = 30_000 };
        var p = new HumanInputNoiseParameters(true, 0.4f, 1f, 0f);
        var c = new HumanInputNoiseController(noise, () => p, time);

        for (var i = 0; i < 50; i++)
        {
            time.Ticks += 16;
            c.AdjustMouseMove(8, 0);
        }

        // Reassign 'p' to simulate a settings change (e.g. user disabled amplitude).
        // The controller closure captures the reference to 'p' (or the variable itself if it's a local closure).
        p = new HumanInputNoiseParameters(true, 0f, 1f, 0f);
        time.Ticks += 16;
        Assert.Equal((5, -2), c.AdjustMouseMove(5, -2));
    }

    [Fact]
    public void MouseMove_ConsecutiveJitterSteps_AreBounded_NoSpikes()
    {
        var noise = new NoiseGenerator(123);
        var time = new SteppingTimeProvider { Ticks = 40_000 };
        var p = new HumanInputNoiseParameters(true, 0.6f, 0.5f, 0f);
        var c = new HumanInputNoiseController(noise, () => p, time);

        int prevJx = 0;
        int prevJy = 0;
        var first = true;

        for (var i = 0; i < 300; i++)
        {
            time.Ticks += 16;
            var (dx, dy) = c.AdjustMouseMove(10, -3);
            var jx = dx - 10;
            var jy = dy - (-3);

            if (first)
            {
                first = false;
                prevJx = jx;
                prevJy = jy;
                continue;
            }

            Assert.True(Math.Abs(jx - prevJx) <= 4, $"jx jump: {prevJx} -> {jx}");
            Assert.True(Math.Abs(jy - prevJy) <= 4, $"jy jump: {prevJy} -> {jy}");
            prevJx = jx;
            prevJy = jy;
        }
    }
}
