using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

public sealed class ProcessTargetService : IProcessTargetService
{
    /// <inheritdoc />
    public ProcessInfo CreateTargetFromDeclaredProcessName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return new ProcessInfo
            {
                ProcessId = 0,
                ProcessName = string.Empty,
                MainWindowTitle = string.Empty
            };
        }

        var trimmed = rawName.Trim();
        var baseName = trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4].Trim()
            : trimmed;

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return new ProcessInfo
            {
                ProcessId = 0,
                ProcessName = string.Empty,
                MainWindowTitle = string.Empty
            };
        }

        var pid = TryGetFirstLiveProcessIdByName(baseName);
        return new ProcessInfo
        {
            ProcessId = pid,
            ProcessName = baseName,
            MainWindowTitle = string.Empty
        };
    }

    private static int TryGetFirstLiveProcessIdByName(string processNameWithoutExtension)
    {
        try
        {
            var processes = Process.GetProcessesByName(processNameWithoutExtension);
            try
            {
                return processes.Length > 0 ? processes[0].Id : 0;
            }
            finally
            {
                foreach (var p in processes)
                    p.Dispose();
            }
        }
        catch
        {
            return 0;
        }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevationClass = 20;

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
        out TOKEN_ELEVATION tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }

    public List<ProcessInfo> GetRecentWindowedProcesses()
    {
        var results = new List<ProcessInfo>();
        try
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase);

            foreach (var p in processes)
            {
                try
                {
                    results.Add(new ProcessInfo
                    {
                        ProcessId = p.Id,
                        ProcessName = p.ProcessName,
                        MainWindowTitle = p.MainWindowTitle
                    });
                }
                catch
                {
                    // Access denied for some system processes.
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch
        {
            // Best-effort enumeration.
        }

        return results;
    }

    public int GetForegroundProcessId()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return 0;

        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    public bool IsForeground(int processId)
    {
        if (processId <= 0)
            return false;

        return GetForegroundProcessId() == processId;
    }

    public bool IsForeground(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return false;

        var fgPid = GetForegroundProcessId();
        if (fgPid <= 0)
            return false;

        try
        {
            var fgProcess = Process.GetProcessById(fgPid);
            return string.Equals(fgProcess.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string GetForegroundWindowTitle()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return string.Empty;

        var buffer = new StringBuilder(512);
        var copied = GetWindowText(hwnd, buffer, buffer.Capacity);
        if (copied <= 0)
            return string.Empty;

        return buffer.ToString();
    }

    public bool IsForeground(ProcessInfo target)
    {
        if (target is null)
            return false;

        if (IsForeground(target.ProcessId))
            return true;

        if (!string.IsNullOrWhiteSpace(target.ProcessName) && IsForeground(target.ProcessName))
            return true;

        if (string.IsNullOrWhiteSpace(target.MainWindowTitle))
            return false;

        var foregroundTitle = GetForegroundWindowTitle();
        if (string.IsNullOrWhiteSpace(foregroundTitle))
            return false;

        return string.Equals(foregroundTitle, target.MainWindowTitle, StringComparison.OrdinalIgnoreCase)
               || foregroundTitle.Contains(target.MainWindowTitle, StringComparison.OrdinalIgnoreCase)
               || target.MainWindowTitle.Contains(foregroundTitle, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsCurrentProcessElevated()
    {
        using var current = Process.GetCurrentProcess();
        return IsProcessHandleElevated(current.Handle, closeProcessHandle: false);
    }

    public bool IsProcessElevated(int processId)
    {
        if (processId <= 0)
            return false;

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == IntPtr.Zero)
            return false;

        try
        {
            return IsProcessHandleElevated(processHandle, closeProcessHandle: false);
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static bool IsProcessHandleElevated(IntPtr processHandle, bool closeProcessHandle)
    {
        if (processHandle == IntPtr.Zero)
            return false;

        if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle) || tokenHandle == IntPtr.Zero)
            return false;

        try
        {
            var ok = GetTokenInformation(
                tokenHandle,
                TokenElevationClass,
                out var elevation,
                Marshal.SizeOf<TOKEN_ELEVATION>(),
                out _);
            return ok && elevation.TokenIsElevated != 0;
        }
        finally
        {
            CloseHandle(tokenHandle);
            if (closeProcessHandle)
                CloseHandle(processHandle);
        }
    }
}
