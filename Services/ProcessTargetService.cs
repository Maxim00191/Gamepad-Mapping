using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Gamepad_Mapping;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Services;

public sealed class ProcessTargetService : IProcessTargetService
{
    private readonly IWin32Service _win32;

    public ProcessTargetService(IWin32Service? win32 = null)
    {
        _win32 = win32 ?? new Win32Service();
    }

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
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to get process by name: {processNameWithoutExtension}", ex);
            return 0;
        }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevationClass = 20;

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
        catch (Exception ex)
        {
            App.Logger.Error("Failed to enumerate windowed processes", ex);
        }

        return results;
    }

    public int GetForegroundProcessId()
    {
        try
        {
            var hwnd = _win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return 0;

            _win32.GetWindowThreadProcessId(hwnd, out var pid);
            return (int)pid;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to get foreground process ID", ex);
            return 0;
        }
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
            using var fgProcess = Process.GetProcessById(fgPid);
            return string.Equals(fgProcess.ProcessName, processName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string GetForegroundWindowTitle()
    {
        try
        {
            var hwnd = _win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return string.Empty;

            var buffer = new StringBuilder(512);
            var copied = _win32.GetWindowText(hwnd, buffer, buffer.Capacity);
            if (copied <= 0)
                return string.Empty;

            return buffer.ToString();
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to get foreground window title", ex);
            return string.Empty;
        }
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
        try
        {
            using var current = Process.GetCurrentProcess();
            return IsProcessHandleElevated(current.Handle, closeProcessHandle: false);
        }
        catch (Exception ex)
        {
            App.Logger.Error("Failed to check if current process is elevated", ex);
            return false;
        }
    }

    public bool IsProcessElevated(int processId)
    {
        if (processId <= 0)
            return false;

        var processHandle = _win32.OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == IntPtr.Zero)
            return false;

        try
        {
            return IsProcessHandleElevated(processHandle, closeProcessHandle: false);
        }
        finally
        {
            _win32.CloseHandle(processHandle);
        }
    }

    private bool IsProcessHandleElevated(IntPtr processHandle, bool closeProcessHandle)
    {
        if (processHandle == IntPtr.Zero)
            return false;

        if (!_win32.OpenProcessToken(processHandle, TokenQuery, out var tokenHandle) || tokenHandle == IntPtr.Zero)
            return false;

        IntPtr elevationPtr = IntPtr.Zero;
        try
        {
            var size = Marshal.SizeOf<TOKEN_ELEVATION>();
            elevationPtr = Marshal.AllocHGlobal(size);
            var ok = _win32.GetTokenInformation(
                tokenHandle,
                TokenElevationClass,
                elevationPtr,
                size,
                out _);
            
            if (!ok) return false;

            var elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(elevationPtr);
            return elevation.TokenIsElevated != 0;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Error checking process elevation", ex);
            return false;
        }
        finally
        {
            if (elevationPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(elevationPtr);
            _win32.CloseHandle(tokenHandle);
            if (closeProcessHandle)
                _win32.CloseHandle(processHandle);
        }
    }
}
