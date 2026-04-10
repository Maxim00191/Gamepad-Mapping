using System;
using System.Runtime.InteropServices;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;

namespace GamepadMapperGUI.Services;

public sealed class Win32Service : IWin32Service
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    IntPtr IWin32Service.GetForegroundWindow() => GetForegroundWindow();

    uint IWin32Service.GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId) => GetWindowThreadProcessId(hWnd, out lpdwProcessId);

    int IWin32Service.GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount) => GetWindowText(hWnd, lpString, nMaxCount);

    IntPtr IWin32Service.OpenProcess(uint processAccess, bool bInheritHandle, int processId) => OpenProcess(processAccess, bInheritHandle, processId);

    bool IWin32Service.OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle) => OpenProcessToken(processHandle, desiredAccess, out tokenHandle);

    bool IWin32Service.GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength)
        => GetTokenInformation(tokenHandle, tokenInformationClass, tokenInformation, tokenInformationLength, out returnLength);

    bool IWin32Service.CloseHandle(IntPtr hObject) => CloseHandle(hObject);

    uint IWin32Service.SendInput(uint nInputs, IntPtr pInputs, int cbSize) => SendInput(nInputs, pInputs, cbSize);

    uint IWin32Service.MapVirtualKey(uint uCode, uint uMapType) => MapVirtualKey(uCode, uMapType);

    string IWin32Service.GetProcessName(int processId)
    {
        if (processId <= 0) return string.Empty;

        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero) return string.Empty;

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            if (QueryFullProcessImageName(handle, 0, buffer, ref size))
            {
                var fullPath = buffer.ToString();
                return System.IO.Path.GetFileNameWithoutExtension(fullPath);
            }
        }
        catch
        {
            // Fallback to legacy method if image name query fails.
        }
        finally
        {
            CloseHandle(handle);
        }

        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(processId);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
