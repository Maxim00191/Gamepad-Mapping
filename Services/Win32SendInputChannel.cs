using System;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

/// <summary>Win32 <c>SendInput</c> / <c>MapVirtualKey</c> implementation of <see cref="ISendInputChannel"/>.</summary>
public sealed class Win32SendInputChannel : ISendInputChannel
{
    private readonly IWin32Service _win32;

    public Win32SendInputChannel(IWin32Service? win32 = null)
    {
        _win32 = win32 ?? new Win32Service();
    }

    public uint SendInput(uint nInputs, nint pInputs, int cbSize) =>
        _win32.SendInput(nInputs, (IntPtr)pInputs, cbSize);

    public uint MapVirtualKeyToScanCode(uint virtualKey) =>
        _win32.MapVirtualKey(virtualKey, 0);
}
