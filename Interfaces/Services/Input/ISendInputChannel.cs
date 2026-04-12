using System;
using System.Collections.Generic;
using GamepadMapperGUI.Services.Win32;

namespace GamepadMapperGUI.Interfaces.Services.Input;

/// <summary>
/// Low-level OS input injection used by <see cref="IKeyboardEmulator"/> / <see cref="IMouseEmulator"/> implementations.
/// Swap this (together with custom emulator types) to replace Win32 <c>SendInput</c> with another backend.
/// </summary>
public interface ISendInputChannel
{
    /// <summary>Injects a sequence of input events.</summary>
    uint SendInput(ReadOnlySpan<INPUT> inputs);

    /// <summary>MapVirtualKey with MAPVK_VK_TO_VSC for keyboard scan codes.</summary>
    uint MapVirtualKeyToScanCode(uint virtualKey);
}

