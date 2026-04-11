using System;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Builds keyboard/mouse emulator pairs for the mapping engine: raw backend emulators always wrapped in human-noise decorators.
/// Actual noise is applied only when <see cref="HumanInputNoiseParameters.Enabled"/> is true (resolved per call via <c>getNoiseParameters</c>).
/// </summary>
public interface IInputEmulationStackFactory
{
    (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreatePair(
        string? inputEmulationApiId,
        Func<HumanInputNoiseParameters> getNoiseParameters);
}
