using System;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapping.Tests.Mocks;

public class MockWin32Service : IWin32Service
{
    public Func<IntPtr> GetForegroundWindowFunc { get; set; } = () => IntPtr.Zero;
    public Func<IntPtr, uint, uint> GetWindowThreadProcessIdFunc { get; set; } = (hwnd, pid) => 0;
    public Func<IntPtr, StringBuilder, int, int> GetWindowTextFunc { get; set; } = (hwnd, sb, max) => 0;
    public Func<uint, bool, int, IntPtr> OpenProcessFunc { get; set; } = (access, inherit, pid) => IntPtr.Zero;
    public Func<IntPtr, uint, IntPtr, bool> OpenProcessTokenFunc { get; set; } = (h, access, token) => false;
    public Func<IntPtr, int, IntPtr, int, int, bool> GetTokenInformationFunc { get; set; } = (h, cls, info, len, ret) => false;
    public Func<IntPtr, bool> CloseHandleFunc { get; set; } = (h) => true;
    public Func<uint, IntPtr, int, uint> SendInputFunc { get; set; } = (n, p, size) => 0;
    public Func<uint, uint, uint> MapVirtualKeyFunc { get; set; } = (code, type) => 0;
    public Func<int, string> GetProcessNameFunc { get; set; } = (pid) => string.Empty;

    public IntPtr GetForegroundWindow() => GetForegroundWindowFunc();
    public uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId)
    {
        lpdwProcessId = 0;
        return GetWindowThreadProcessIdFunc(hWnd, lpdwProcessId);
    }
    public int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount) => GetWindowTextFunc(hWnd, lpString, nMaxCount);
    public IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId) => OpenProcessFunc(processAccess, bInheritHandle, processId);
    public bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle)
    {
        tokenHandle = IntPtr.Zero;
        return OpenProcessTokenFunc(processHandle, desiredAccess, tokenHandle);
    }
    public bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength)
    {
        returnLength = 0;
        return GetTokenInformationFunc(tokenHandle, tokenInformationClass, tokenInformation, tokenInformationLength, returnLength);
    }
    public bool CloseHandle(IntPtr hObject) => CloseHandleFunc(hObject);
    public uint SendInput(uint nInputs, IntPtr pInputs, int cbSize) => SendInputFunc(nInputs, pInputs, cbSize);
    public uint MapVirtualKey(uint uCode, uint uMapType) => MapVirtualKeyFunc(uCode, uMapType);
    public string GetProcessName(int processId) => GetProcessNameFunc(processId);
}
