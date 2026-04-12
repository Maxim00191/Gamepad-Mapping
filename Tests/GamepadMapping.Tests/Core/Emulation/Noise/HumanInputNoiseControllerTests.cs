using System.Diagnostics;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Core.Emulation.Noise;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Core.Emulation;
using GamepadMapperGUI.Models;
using Xunit;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class HumanInputNoiseControllerTests
{
    private sealed class SteppingTimeProvider : ITimeProvider
    {
        public long Ticks;

        public long GetTickCount64() => Ticks;

        public long GetPerformanceTimestamp() => (Ticks * Stopwatch.Frequency) / 1000;

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
        var p = new HumanInputNoiseParameters(true, 1.0f, 1f, 0f); // Max amplitude
        var c = new HumanInputNoiseController(noise, () => p, time);

        var sumX = 0;
        for (var i = 0; i < 1000; i++) // More iterations
        {
            time.Ticks += 16;
            var (dx, _) = c.AdjustMouseMove(5, 0);
            sumX += dx - 5;
        }

        // With the fix, sumX is the total displacement.
        // If the noise at the end is different from the noise at the beginning, sumX should be non-zero.
        // Given Perlin noise and 1000 steps, it's extremely likely to be non-zero at some point.
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

    [Fact]
    public void MouseMove_LongTermDrift_IsZero_OnStaticNoise()
    {
        // If noise is static (e.g. frequency 0 or we don't advance time), 
        // the cursor should reach a fixed offset and stay there (delta jitter = 0).
        // The old code would have integrated that offset forever, causing infinite drift.
        
        var noise = new MockNoiseGenerator(0.5f); // Constant 0.5
        var time = new SteppingTimeProvider { Ticks = 50_000 };
        var p = new HumanInputNoiseParameters(true, 1.0f, 0.0f, 0.0f); // Enabled, Amp 1, Freq 0
        var c = new HumanInputNoiseController(noise, () => p, time);

        // First call to establish initial position
        time.Ticks += 16;
        c.AdjustMouseMove(0, 0);

        // Subsequent calls should eventually result in 0 jitter
        var totalJitterAfterSettling = 0;
        for (var i = 0; i < 100; i++)
        {
            time.Ticks += 16;
            var (dx, _) = c.AdjustMouseMove(0, 0);
            if (i > 50) // Allow some frames for smoothing to settle
            {
                totalJitterAfterSettling += Math.Abs(dx);
            }
        }

        Assert.Equal(0, totalJitterAfterSettling);
    }

    [Fact]
    public void Disabled_AdjustTapHoldMs_ReturnsNominal()
    {
        var noise = new NoiseGenerator(1);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(false, 1f, 1f, 0f), time);
        Assert.Equal(63, c.AdjustTapHoldMs(63, 10));
    }

    [Fact]
    public void MaxDeviationZero_AdjustTapHoldMs_ReturnsNominal()
    {
        var noise = new NoiseGenerator(1);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(true, 1f, 1f, 0f), time);
        Assert.Equal(80, c.AdjustTapHoldMs(80, 0));
    }

    [Fact]
    public void Enabled_AdjustTapHoldMs_Nominal50_StaysWithinPlusMinus10BeforeEnvelope()
    {
        var noise = new NoiseGenerator(42);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(true, 0.5f, 0.5f, 0f), time);
        for (var i = 0; i < 400; i++)
        {
            var r = c.AdjustTapHoldMs(50, 10);
            Assert.InRange(r, 40, 60);
        }
    }

    [Fact]
    public void Enabled_AdjustTapHoldMs_Nominal20_ClampedTo20To30()
    {
        var noise = new NoiseGenerator(99);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(true, 1f, 1f, 0f), time);
        for (var i = 0; i < 400; i++)
        {
            var r = c.AdjustTapHoldMs(20, 10);
            Assert.InRange(r, 20, 30);
        }
    }

    [Fact]
    public void Enabled_AdjustTapHoldMs_Nominal100_ClampedTo90To100()
    {
        var noise = new NoiseGenerator(7);
        var time = new RealTimeProvider();
        var c = new HumanInputNoiseController(noise, () => new HumanInputNoiseParameters(true, 1f, 1f, 0f), time);
        for (var i = 0; i < 400; i++)
        {
            var r = c.AdjustTapHoldMs(100, 10);
            Assert.InRange(r, 90, 100);
        }
    }

    private sealed class MockNoiseGenerator(float constantValue) : INoiseGenerator
    {
        public float Sample1D(float x, float y = 0f) => constantValue;
        public float Sample2D(float x, float y) => constantValue;
    }
}
