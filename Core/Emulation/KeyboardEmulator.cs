using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Services;

namespace GamepadMapperGUI.Core
{
    public sealed class KeyboardEmulator : IKeyboardEmulator
    {
        private readonly IWin32Service _win32;
        private readonly object _sendLock = new();
        private const int DefaultTapHoldMs = 30;
        private const int MinTapHoldMs = 20;
        private const int MaxTapHoldMs = 50;

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        public KeyboardEmulator(IWin32Service? win32 = null)
        {
            _win32 = win32 ?? new Win32Service();
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
                // Brief key hold improves detection in engines that sample input state per-frame.
                Thread.Sleep(effectiveHoldMs);
                KeyUp(key);

                if (interKeyDelayMs > 0 && i < repeatCount - 1)
                    Thread.Sleep(interKeyDelayMs);
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

            lock (_sendLock)
            {
                foreach (var m in modList)
                    KeyDown(m);

                KeyDown(mainKey);
                Thread.Sleep(effectiveHoldMs);
                KeyUp(mainKey);

                for (var i = modList.Count - 1; i >= 0; i--)
                    KeyUp(modList[i]);
            }
        }

        public void SendText(string text, int interCharDelayMs = 0)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            // SendInput with KEYEVENTF_UNICODE injects characters for the foreground app.
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
            var scanCode = (ushort)_win32.MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);
            if (scanCode == 0)
            {
                // Fallback for uncommon keys if scan code translation fails.
                var vkFlags = keyUp ? KEYEVENTF_KEYUP : 0;
                SendSingleKeyboardInput(virtualKey, 0, vkFlags);
                return;
            }

            var scanFlags = KEYEVENTF_SCANCODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            if (IsExtendedVirtualKey(virtualKey))
                scanFlags |= KEYEVENTF_EXTENDEDKEY;

            // MSDN: when KEYEVENTF_SCANCODE is set, wVk should be 0 and wScan carries the hardware code.
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
            // Serialize to avoid interleaving partial sequences from multiple callers.
            lock (_sendLock)
            {
                var input = new INPUT
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

                var size = Marshal.SizeOf<INPUT>();
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(input, ptr, false);
                    var sent = _win32.SendInput(1, ptr, size);
                    if (sent != 1)
                    {
                        var err = Marshal.GetLastWin32Error();
                        App.Logger.Warning($"SendInput failed. key={wVk:X4} scan={wScan:X4} flags=0x{flags:X} err={err}");
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Exception during SendInput", ex);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }
}
