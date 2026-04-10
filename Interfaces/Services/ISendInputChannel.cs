namespace GamepadMapperGUI.Interfaces.Services;

/// <summary>
/// Low-level OS input injection used by <see cref="IKeyboardEmulator"/> / <see cref="IMouseEmulator"/> implementations.
/// Swap this (together with custom emulator types) to replace Win32 <c>SendInput</c> with another backend.
/// </summary>
public interface ISendInputChannel
{
    uint SendInput(uint nInputs, nint pInputs, int cbSize);

    /// <summary>MapVirtualKey with MAPVK_VK_TO_VSC for keyboard scan codes.</summary>
    uint MapVirtualKeyToScanCode(uint virtualKey);
}
