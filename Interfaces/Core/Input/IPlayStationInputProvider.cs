using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

/// <summary>
/// Provides native PlayStation controller input frames (DualSense/DualSense Edge).
/// </summary>
public interface IPlayStationInputProvider
{
    bool TryGetState(out PlayStationInputState state);
}
