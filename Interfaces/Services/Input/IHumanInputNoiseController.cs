using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Stateful human-like jitter for delays and mouse movement. Implementations read current
/// <see cref="HumanInputNoiseParameters"/> from a delegate so sliders apply without rebuilding the stack.
/// </summary>
public interface IHumanInputNoiseController
{
    int AdjustDelayMs(int baseDelayMs);

    (int Dx, int Dy) AdjustMouseMove(int deltaX, int deltaY);
}
