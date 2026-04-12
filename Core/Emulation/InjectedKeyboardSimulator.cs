using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI.Input.Preview.Injection;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services.Input;
using Gamepad_Mapping;

namespace GamepadMapperGUI.Core.Emulation;

/// <summary>
/// Keyboard output via <see cref="InputInjector"/> (Windows.UI.Input.Preview.Injection).
/// </summary>
public sealed class InjectedKeyboardSimulator : IKeyboardEmulator
{
    private readonly InputInjector? _injector;
    private readonly ISendInputChannel _sendChannel;
    private const int DefaultTapHoldMs = 70;

    public InjectedKeyboardSimulator(ISendInputChannel? sendChannel = null)
    {
        _sendChannel = sendChannel ?? new Win32SendInputChannel();
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

    public void Execute(OutputCommand command)
    {
        switch (command.Type)
        {
            case OutputCommandType.KeyPress: KeyDown(command.Key); break;
            case OutputCommandType.KeyRelease: KeyUp(command.Key); break;
            case OutputCommandType.KeyTap: TapKey(command.Key, keyHoldMs: command.Metadata > 0 ? command.Metadata : DefaultTapHoldMs); break;
            case OutputCommandType.Text: SendText(command.Text ?? string.Empty); break;
        }
    }

    public async Task ExecuteAsync(OutputCommand command, CancellationToken cancellationToken = default)
    {
        switch (command.Type)
        {
            case OutputCommandType.KeyPress: KeyDown(command.Key); break;
            case OutputCommandType.KeyRelease: KeyUp(command.Key); break;
            case OutputCommandType.KeyTap: await TapKeyAsync(command.Key, keyHoldMs: command.Metadata > 0 ? command.Metadata : DefaultTapHoldMs, cancellationToken: cancellationToken).ConfigureAwait(false); break;
            case OutputCommandType.Text: SendText(command.Text ?? string.Empty); break;
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
        if (mainKey == Key.None || modifiers is null) return;

        foreach (var mod in modifiers)
        {
            if (mod != Key.None) KeyDown(mod);
        }

        KeyDown(mainKey);
        if (keyHoldMs > 0) Thread.Sleep(keyHoldMs);
        KeyUp(mainKey);

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i] != Key.None) KeyUp(modifiers[i]);
        }
    }

    public async Task TapKeyChordAsync(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs, CancellationToken cancellationToken = default)
    {
        if (mainKey == Key.None || modifiers is null) return;

        foreach (var mod in modifiers)
        {
            if (mod != Key.None) KeyDown(mod);
        }

        KeyDown(mainKey);
        await Task.Delay(keyHoldMs, cancellationToken).ConfigureAwait(false);
        KeyUp(mainKey);

        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (modifiers[i] != Key.None) KeyUp(modifiers[i]);
        }
    }

    public void SendText(string text, int interCharDelayMs = 0)
    {
        if (string.IsNullOrEmpty(text)) return;

        // InputInjector doesn't have a direct "SendUnicode" like Win32 SendInput with KEYEVENTF_UNICODE.
        // We would need to map chars to keys or use a different approach if full unicode is needed.
        // For now, we'll skip or implement basic mapping if required.
    }

    public void TapKeys(IEnumerable<Key> keys)
    {
        if (keys is null) return;
        var keyList = new List<Key>(keys);
        foreach (var key in keyList)
        {
            if (key != Key.None) KeyDown(key);
        }
        foreach (var key in keyList)
        {
            if (key != Key.None) KeyUp(key);
        }
    }

    private InjectedInputKeyboardInfo CreateKeyInfo(Key key, bool isUp)
    {
        var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        var options = isUp ? InjectedInputKeyOptions.KeyUp : InjectedInputKeyOptions.None;

        if (vk != 0)
        {
            var scan = _sendChannel.MapVirtualKeyToScanCode(vk);
            if (scan != 0)
            {
                if (IsExtendedKey(vk))
                    options |= InjectedInputKeyOptions.ExtendedKey;
                options |= InjectedInputKeyOptions.ScanCode;
                return new InjectedInputKeyboardInfo
                {
                    VirtualKey = 0,
                    ScanCode = (ushort)scan,
                    KeyOptions = options
                };
            }
        }

        if (IsExtendedKey(vk))
            options |= InjectedInputKeyOptions.ExtendedKey;

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
