using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Core;

/// <summary>
/// Represents a protocol-specific source of gamepad data (e.g., XInput, DualSense).
/// </summary>
public interface IGamepadSource
{
    /// <summary>
    /// Attempts to read the current state of the gamepad.
    /// </summary>
    bool TryGetFrame(out InputFrame frame);
}
