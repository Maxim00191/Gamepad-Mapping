using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Stateful human-like jitter for delays and mouse movement. Implementations read current
/// <see cref="HumanInputNoiseParameters"/> from a delegate so sliders apply without rebuilding the stack.
/// </summary>
public interface IHumanInputNoiseController
{
    int AdjustDelayMs(int baseDelayMs);

    /// <summary>
    /// Additive jitter for keyboard tap hold (±<paramref name="maxDeviationMs"/> ms from Perlin), then clamp to the app envelope.
    /// When human noise is off, returns <paramref name="nominalMs"/> unchanged.
    /// </summary>
    int AdjustTapHoldMs(int nominalMs, int maxDeviationMs);

    (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY, float stickMagnitude = 1.0f);
}
