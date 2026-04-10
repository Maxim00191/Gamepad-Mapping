using System;
using System.Runtime.InteropServices;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Services.Win32;

namespace GamepadMapperGUI.Services;

/// <summary>Win32 <c>SendInput</c> / <c>MapVirtualKey</c> implementation of <see cref="ISendInputChannel"/>.</summary>
public sealed class Win32SendInputChannel : ISendInputChannel
{
    private readonly IWin32Service _win32;

    public Win32SendInputChannel(IWin32Service? win32 = null)
    {
        _win32 = win32 ?? new Win32Service();
    }

    public uint SendInput(ReadOnlySpan<INPUT> inputs)
    {
        if (inputs.IsEmpty) return 0;

        var size = Marshal.SizeOf<INPUT>();
        IntPtr ptr = Marshal.AllocHGlobal(size * inputs.Length);
        try
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                Marshal.StructureToPtr(inputs[i], ptr + (i * size), false);
            }
            return _win32.SendInput((uint)inputs.Length, ptr, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public uint MapVirtualKeyToScanCode(uint virtualKey) =>
        _win32.MapVirtualKey(virtualKey, 0);
}
