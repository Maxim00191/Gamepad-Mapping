using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

/// <summary>Factory for keyboard/mouse emulation backends.</summary>
public static class InputEmulationServices
{
    /// <summary>Creates paired emulators sharing one <see cref="ISendInputChannel"/> (one Win32 hook-up point).</summary>
    public static (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreateWin32(ISendInputChannel? sendChannel = null)
    {
        var channel = sendChannel ?? new Win32SendInputChannel();
        return (new Win32KeyboardEmulator(channel), new Win32MouseEmulator(channel));
    }

    /// <summary>Resolves <paramref name="apiId"/> to implementations. Currently only <see cref="InputEmulationApiIds.Win32"/>; other values fall back to Win32 until more backends exist.</summary>
    public static (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreatePair(string? apiId)
    {
        var id = apiId?.Trim();
        if (string.IsNullOrEmpty(id) || string.Equals(id, InputEmulationApiIds.Win32, StringComparison.OrdinalIgnoreCase))
            return CreateWin32();
        return CreateWin32();
    }
}
