using System;
using System.Runtime.InteropServices;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Services.Win32;

namespace GamepadMapperGUI.Services.Input;

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


