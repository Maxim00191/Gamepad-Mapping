using System;
using System.Collections.Generic;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

/// <summary>Factory for keyboard/mouse emulation backends.</summary>
public static class InputEmulationServices
{
    private static readonly Dictionary<string, Func<(IKeyboardEmulator Keyboard, IMouseEmulator Mouse)>> _registry = 
        new(StringComparer.OrdinalIgnoreCase);

    static InputEmulationServices()
    {
        // Register default Win32 backend
        Register(InputEmulationApiIds.Win32, () => CreateWin32());
    }

    /// <summary>Registers a new emulation backend.</summary>
    public static void Register(string apiId, Func<(IKeyboardEmulator Keyboard, IMouseEmulator Mouse)> factory)
    {
        _registry[apiId] = factory;
    }

    /// <summary>Creates paired emulators sharing one <see cref="ISendInputChannel"/> (one Win32 hook-up point).</summary>
    public static (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreateWin32(ISendInputChannel? sendChannel = null)
    {
        var channel = sendChannel ?? new Win32SendInputChannel();
        return (new Win32KeyboardEmulator(channel), new Win32MouseEmulator(channel));
    }

    /// <summary>Resolves <paramref name="apiId"/> to implementations. Falls back to Win32 if not found.</summary>
    public static (IKeyboardEmulator Keyboard, IMouseEmulator Mouse) CreatePair(string? apiId)
    {
        var id = apiId?.Trim() ?? string.Empty;
        if (_registry.TryGetValue(id, out var factory))
        {
            return factory();
        }

        // Fallback to Win32
        return CreateWin32();
    }
}
