namespace GamepadMapperGUI.Interfaces.Core.Emulation;

/// <summary>Coherent gradient noise for humanizing timing or motion (e.g. Perlin).</summary>
public interface INoiseGenerator
{
    float Sample1D(float x, float y = 0f);

    float Sample2D(float x, float y);
}
