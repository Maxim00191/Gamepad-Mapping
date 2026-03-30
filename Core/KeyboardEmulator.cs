using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace GamepadMapperGUI.Core
{
    public sealed class KeyboardEmulator
    {
        private readonly object _sendLock = new();

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
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

        public void KeyDown(Key key)
        {
            if (key == Key.None)
                throw new ArgumentException("Key cannot be Key.None.", nameof(key));

            var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0)
                throw new ArgumentException($"Unsupported key for Win32 virtual-key mapping: {key}", nameof(key));

            SendSingleKeyboardInput(vk, 0, flags: 0);
        }

        public void KeyUp(Key key)
        {
            if (key == Key.None)
                throw new ArgumentException("Key cannot be Key.None.", nameof(key));

            var vk = (ushort)KeyInterop.VirtualKeyFromKey(key);
            if (vk == 0)
                throw new ArgumentException($"Unsupported key for Win32 virtual-key mapping: {key}", nameof(key));

            SendSingleKeyboardInput(vk, 0, flags: KEYEVENTF_KEYUP);
        }

        public void TapKey(Key key, int repeatCount = 1, int interKeyDelayMs = 0)
        {
            if (repeatCount < 1)
                throw new ArgumentOutOfRangeException(nameof(repeatCount), "repeatCount must be >= 1.");

            for (var i = 0; i < repeatCount; i++)
            {
                KeyDown(key);
                KeyUp(key);

                if (interKeyDelayMs > 0 && i < repeatCount - 1)
                    Thread.Sleep(interKeyDelayMs);
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

                var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                if (sent != 1)
                {
                    var err = Marshal.GetLastWin32Error();
                    Debug.WriteLine($"SendInput failed. key={wVk:X4} scan={wScan:X4} flags=0x{flags:X} err={err}");
                }
            }
        }
    }
}

