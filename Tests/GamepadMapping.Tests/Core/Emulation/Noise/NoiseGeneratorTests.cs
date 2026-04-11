using GamepadMapperGUI.Core.Emulation.Noise;

namespace GamepadMapping.Tests.Core.Emulation.Noise;

public sealed class NoiseGeneratorTests
{
    [Fact]
    public void SameSeed_ProducesSameSamples()
    {
        var a = new NoiseGenerator(42);
        var b = new NoiseGenerator(42);

        Assert.Equal(a.Sample2D(1.23f, 4.56f), b.Sample2D(1.23f, 4.56f), 5);
        Assert.Equal(a.Sample1D(9.87f), b.Sample1D(9.87f), 5);
    }

    [Fact]
    public void DifferentSeeds_CanDiffer()
    {
        var a = new NoiseGenerator(1);
        var b = new NoiseGenerator(2);

        var any = false;
        for (float x = 0.1f; x < 4f && !any; x += 0.37f)
        {
            for (float y = 0.1f; y < 4f; y += 0.41f)
            {
                if (MathF.Abs(a.Sample2D(x, y) - b.Sample2D(x, y)) > 1e-4f)
                {
                    any = true;
                    break;
                }
            }
        }

        Assert.True(any);
    }

    [Fact]
    public void SamplesStayRoughlyBounded()
    {
        var n = new NoiseGenerator(12345);
        for (float x = -20f; x < 20f; x += 0.7f)
        {
            for (float y = -20f; y < 20f; y += 0.6f)
            {
                float v = n.Sample2D(x, y);
                Assert.InRange(v, -1.05f, 1.05f);
            }
        }
    }

    [Fact]
    public void NearbyInputsYieldNearbyOutputs()
    {
        var n = new NoiseGenerator(99);
        float c = n.Sample2D(100f, 100f);
        float near = n.Sample2D(100.02f, 100.02f);
        Assert.True(MathF.Abs(c - near) < 0.2f);
    }

    [Fact]
    public void ConsecutiveSamplesAlongAxis_DoNotJumpLikeWhiteNoise()
    {
        var n = new NoiseGenerator(42);
        const float step = 0.04f;
        const float maxDelta = 0.22f;

        float prev = n.Sample2D(0f, 1.7f);
        for (float x = step; x < 80f; x += step)
        {
            float cur = n.Sample2D(x, 1.7f);
            Assert.True(MathF.Abs(cur - prev) <= maxDelta, $"Sample jump at x={x}: {prev} -> {cur}");
            prev = cur;
        }
    }
}
