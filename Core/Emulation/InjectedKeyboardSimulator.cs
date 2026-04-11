using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Input.Preview.Injection;
using GamepadMapperGUI.Interfaces.Services.Input;
using Gamepad_Mapping;

namespace GamepadMapperGUI.Core.Emulation;

/// <summary>
/// Keyboard output via <see cref="InputInjector"/> (Windows.UI.Input.Preview.Injection).
/// </summary>
public sealed class InjectedKeyboardSimulator : IKeyboardEmulator
{
    private readonly InputInjector? _injector;
    private const int DefaultTapHoldMs = 30;

    public InjectedKeyboardSimulator()
    {
        try
        {
            _injector = InputInjector.TryCreate();
            if (_injector == null)
            {
                App.Logger.Error("InputInjector.TryCreate() returned null. Input injection will not work (requires UI access/trusted process).");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to initialize InputInjector: {ex.Message}");
            _injector = null;
        }
    }

    public void KeyDown(Key key)
    {
        if (_injector == null || key == Key.None) return;
        var info = CreateKeyInfo(key, false);
        _injector.InjectKeyboardInput([info]);
    }

    public void KeyUp(Key key)
    {
        if (_injector == null || key == Key.None) return;
        var info = CreateKeyInfo(key, true);
        _injector.InjectKeyboardInput([info]);
    }

    public void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = DefaultTapHoldMs)
    {
        if (key == Key.None || repeatCount < 1) return;

        for (int i = 0; i < repeatCount; i++)
        {
            KeyDown(key);
            if (keyHoldMs > 0) Thread.Sleep(keyHoldMs);
            KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
                Thread.Sleep(interKeyDelayMs);
        }
    }

    public async Task TapKeyAsync(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = DefaultTapHoldMs, CancellationToken cancellationToken = default)
    {
        if (key == Key.None || repeatCount < 1) return;

        for (int i = 0; i < repeatCount; i++)
        {
            KeyDown(key);
            await Task.Delay(keyHoldMs, cancellationToken).ConfigureAwait(false);
            KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
                await Task.Delay(interKeyDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    public void TapKeyChord(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs)
    {
        if (mainKey == Key.None) return;

        foreach (var mod in modifiers) KeyDown(mod);
        KeyDown(mainKey);
        if (keyHoldMs > 0) Thread.Sleep(keyHoldMs);
        KeyUp(mainKey);
        for (int i = modifiers.Count - 1; i >= 0; i--) KeyUp(modifiers[i]);
    }

    public async Task TapKeyChordAsync(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs, CancellationToken cancellationToken = default)
    {
        if (mainKey == Key.None) return;

        foreach (var mod in modifiers) KeyDown(mod);
        KeyDown(mainKey);
        await Task.Delay(keyHoldMs, cancellationToken).ConfigureAwait(false);
        KeyUp(mainKey);
        for (int i = modifiers.Count - 1; i >= 0; i--) KeyUp(modifiers[i]);
    }

    public void SendText(string text, int interCharDelayMs = 0)
    {
        if (string.IsNullOrEmpty(text)) return;

        foreach (var ch in text)
        {
            // InputInjector doesn't have a direct "SendUnicode" like Win32 SendInput with KEYEVENTF_UNICODE.
            // We would need to map chars to keys or use a different approach if full unicode is needed.
            // For now, we'll skip or implement basic mapping if required.
        }
    }

    public void TapKeys(IEnumerable<Key> keys)
    {
        var keyList = new List<Key>(keys);
        foreach (var key in keyList) KeyDown(key);
        foreach (var key in keyList) KeyUp(key);
    }

    private static InjectedInputKeyboardInfo CreateKeyInfo(Key key, bool isUp)
    {
        // KeyInterop.VirtualKeyFromKey returns the standard Win32 VK code.
        // Windows.System.VirtualKey (used by InputInjector) values are identical to Win32 VK codes 
        // for almost all keys, but we cast to (Windows.System.VirtualKey) to be explicit.
        var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        
        var options = isUp ? InjectedInputKeyOptions.KeyUp : InjectedInputKeyOptions.None;

        // Handle extended keys if necessary. InputInjector handles most mapping automatically,
        // but some specific keys (like Right Alt/Control) might need ScanCode or ExtendedKey flags
        // if the VirtualKey alone isn't sufficient for the target app.
        if (IsExtendedKey(vk))
        {
            options |= InjectedInputKeyOptions.ExtendedKey;
        }

        return new InjectedInputKeyboardInfo
        {
            VirtualKey = vk,
            KeyOptions = options
        };
    }

    private static bool IsExtendedKey(ushort virtualKey)
    {
        // Standard Win32 extended keys
        return virtualKey is
            0x21 or // VK_PRIOR (PageUp)
            0x22 or // VK_NEXT (PageDown)
            0x23 or // VK_END
            0x24 or // VK_HOME
            0x25 or // VK_LEFT
            0x26 or // VK_UP
            0x27 or // VK_RIGHT
            0x28 or // VK_DOWN
            0x2D or // VK_INSERT
            0x2E or // VK_DELETE
            0x6F or // VK_DIVIDE (numpad /)
            0x90 or // VK_NUMLOCK
            0xA3 or // VK_RCONTROL
            0xA5;   // VK_RMENU (Right Alt)
    }
}
