using System;
using GamepadMapperGUI.Interfaces.Core.Emulation;

namespace GamepadMapperGUI.Core.Emulation.Noise;

/// <summary>
/// 2D gradient (Perlin) noise: smooth, spatially coherent values for jittering timing or motion.
/// Output is approximately in <c>[-1, 1]</c>; scale in the caller as needed.
/// </summary>
public sealed class NoiseGenerator : INoiseGenerator
{
    private readonly int[] _perm = new int[512];

    public NoiseGenerator() : this(Environment.TickCount)
    {
    }

    public NoiseGenerator(int seed)
    {
        Span<int> order = stackalloc int[256];
        for (int i = 0; i < 256; i++)
            order[i] = i;

        var random = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        for (int i = 0; i < 256; i++)
            _perm[i] = _perm[i + 256] = order[i];
    }

    /// <summary>Sample along one axis; <paramref name="y"/> is a fixed slice for independent 1D streams.</summary>
    public float Sample1D(float x, float y = 0f) => Sample2D(x, y);

    /// <summary>2D Perlin noise at <paramref name="x"/>, <paramref name="y"/>.</summary>
    public float Sample2D(float x, float y)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;

        float xf = x - (float)Math.Floor(x);
        float yf = y - (float)Math.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = _perm[xi] + yi;
        int ab = _perm[xi] + yi + 1;
        int ba = _perm[xi + 1] + yi;
        int bb = _perm[xi + 1] + yi + 1;

        float g00 = Grad(_perm[aa], xf, yf);
        float g10 = Grad(_perm[ba], xf - 1f, yf);
        float g01 = Grad(_perm[ab], xf, yf - 1f);
        float g11 = Grad(_perm[bb], xf - 1f, yf - 1f);

        float x1 = Lerp(u, g00, g10);
        float x2 = Lerp(u, g01, g11);
        return Lerp(v, x1, x2);
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float t, float a, float b) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 7;
        return h switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            3 => -x - y,
            4 => x,
            5 => -x,
            6 => y,
            _ => -y,
        };
    }
}
