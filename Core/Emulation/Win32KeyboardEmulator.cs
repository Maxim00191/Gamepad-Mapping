using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;

using GamepadMapperGUI.Services.Win32;
using static GamepadMapperGUI.Services.Win32.Win32InputConstants;

namespace GamepadMapperGUI.Core;

/// <summary>Keyboard output via Win32 <c>SendInput</c> (<see cref="ISendInputChannel"/>).</summary>
public sealed class Win32KeyboardEmulator : IKeyboardEmulator
{
    private readonly ISendInputChannel _sendChannel;
    private readonly object _sendLock = new();
    private readonly SemaphoreSlim _chordSequenceGate = new(1, 1);
    private const int DefaultTapHoldMs = 30;
    private const int MinTapHoldMs = 20;
    private const int MaxTapHoldMs = 50;

    public Win32KeyboardEmulator(ISendInputChannel? sendChannel = null)
    {
        _sendChannel = sendChannel ?? new Win32SendInputChannel();
    }

    public void KeyDown(Key key)
    {
        if (key == Key.None)
            throw new ArgumentException("Key cannot be Key.None.", nameof(key));

        var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
            throw new ArgumentException($"Unsupported key for Win32 virtual-key mapping: {key}", nameof(key));

        SendKeyboardKey(vk, keyUp: false);
    }

    public void KeyUp(Key key)
    {
        if (key == Key.None)
            throw new ArgumentException("Key cannot be Key.None.", nameof(key));

        var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
            throw new ArgumentException($"Unsupported key for Win32 virtual-key mapping: {key}", nameof(key));

        SendKeyboardKey(vk, keyUp: true);
    }

    public void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0, int keyHoldMs = DefaultTapHoldMs)
    {
        if (repeatCount < 1)
            throw new ArgumentOutOfRangeException(nameof(repeatCount), "repeatCount must be >= 1.");

        var effectiveHoldMs = Math.Clamp(keyHoldMs, MinTapHoldMs, MaxTapHoldMs);
        for (var i = 0; i < repeatCount; i++)
        {
            KeyDown(key);
            if (effectiveHoldMs > 0) Thread.Sleep(effectiveHoldMs);
            KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
                Thread.Sleep(interKeyDelayMs);
        }
    }

    public async Task TapKeyAsync(
        Key key,
        int repeatCount = 1,
        int interKeyDelayMs = 0,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default)
    {
        if (repeatCount < 1)
            throw new ArgumentOutOfRangeException(nameof(repeatCount), "repeatCount must be >= 1.");

        var effectiveHoldMs = Math.Clamp(keyHoldMs, MinTapHoldMs, MaxTapHoldMs);
        for (var i = 0; i < repeatCount; i++)
        {
            KeyDown(key);
            await Task.Delay(effectiveHoldMs, cancellationToken).ConfigureAwait(false);
            KeyUp(key);

            if (interKeyDelayMs > 0 && i < repeatCount - 1)
                await Task.Delay(interKeyDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    public void TapKeyChord(IReadOnlyList<Key> modifiers, Key mainKey, int keyHoldMs = DefaultTapHoldMs)
    {
        if (mainKey == Key.None)
            throw new ArgumentException("Main key cannot be Key.None.", nameof(mainKey));
        if (modifiers is null) throw new ArgumentNullException(nameof(modifiers));

        var effectiveHoldMs = Math.Clamp(keyHoldMs, MinTapHoldMs, MaxTapHoldMs);
        var modList = new List<Key>();
        foreach (var k in modifiers)
        {
            if (k != Key.None)
                modList.Add(k);
        }

        _chordSequenceGate.Wait(CancellationToken.None);
        try
        {
            foreach (var m in modList)
                KeyDown(m);

            KeyDown(mainKey);
            if (effectiveHoldMs > 0) Thread.Sleep(effectiveHoldMs);
            KeyUp(mainKey);

            for (var i = modList.Count - 1; i >= 0; i--)
                KeyUp(modList[i]);
        }
        finally
        {
            _chordSequenceGate.Release();
        }
    }

    public async Task TapKeyChordAsync(
        IReadOnlyList<Key> modifiers,
        Key mainKey,
        int keyHoldMs = DefaultTapHoldMs,
        CancellationToken cancellationToken = default)
    {
        if (mainKey == Key.None)
            throw new ArgumentException("Main key cannot be Key.None.", nameof(mainKey));
        if (modifiers is null) throw new ArgumentNullException(nameof(modifiers));

        var effectiveHoldMs = Math.Clamp(keyHoldMs, MinTapHoldMs, MaxTapHoldMs);
        var modList = new List<Key>();
        foreach (var k in modifiers)
        {
            if (k != Key.None)
                modList.Add(k);
        }

        await _chordSequenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var m in modList)
                KeyDown(m);

            KeyDown(mainKey);
            await Task.Delay(effectiveHoldMs, cancellationToken).ConfigureAwait(false);
            KeyUp(mainKey);

            for (var i = modList.Count - 1; i >= 0; i--)
                KeyUp(modList[i]);
        }
        finally
        {
            _chordSequenceGate.Release();
        }
    }

    public void SendText(string text, int interCharDelayMs = 0)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        lock (_sendLock)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                SendUnicodeChar(ch, keyUp: false);
                SendUnicodeChar(ch, keyUp: true);

                if (interCharDelayMs > 0 && i < text.Length - 1)
                    Thread.Sleep(interCharDelayMs);
            }
        }
    }

    public void TapKeys(IEnumerable<Key> keys)
    {
        if (keys is null) throw new ArgumentNullException(nameof(keys));

        lock (_sendLock)
        {
            foreach (var key in keys)
            {
                KeyDown(key);
            }

            foreach (var key in keys)
            {
                KeyUp(key);
            }
        }
    }

    private void SendUnicodeChar(char ch, bool keyUp)
    {
        var wVk = (ushort)0;
        var wScan = (ushort)ch;
        var flags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0);

        SendSingleKeyboardInput(wVk, wScan, flags);
    }

    private void SendKeyboardKey(ushort virtualKey, bool keyUp)
    {
        var scanCode = (ushort)_sendChannel.MapVirtualKeyToScanCode(virtualKey);
        if (scanCode == 0)
        {
            var vkFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            SendSingleKeyboardInput(virtualKey, 0, vkFlags);
            return;
        }

        var scanFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
        if (IsExtendedVirtualKey(virtualKey))
            scanFlags |= KEYEVENTF_EXTENDEDKEY;

        SendSingleKeyboardInput(0, scanCode, scanFlags);
    }

    private static bool IsExtendedVirtualKey(ushort virtualKey)
    {
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

    private void SendSingleKeyboardInput(ushort wVk, ushort wScan, uint flags)
    {
        lock (_sendLock)
        {
            Span<INPUT> inputs = stackalloc INPUT[1];
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = wVk,
                        wScan = wScan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var sent = _sendChannel.SendInput(inputs);
            if (sent != 1)
            {
                var err = Marshal.GetLastWin32Error();
                App.Logger.Warning($"SendInput failed. key={wVk:X4} scan={wScan:X4} flags=0x{flags:X} err={err}");
            }
        }
    }
}


