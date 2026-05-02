#nullable enable

using System.Diagnostics;
using System.Runtime.InteropServices;
using GamepadMapperGUI.Interfaces.Services.Automation;
using GamepadMapperGUI.Models.Automation;

namespace GamepadMapperGUI.Services.Automation;

public sealed class AutomationProcessWindowResolver : IAutomationProcessWindowResolver
{
    public bool TryResolveWindowHandle(
        string? processName,
        out IntPtr windowHandle,
        out AutomationPhysicalRect windowBounds)
    {
        var target = AutomationProcessWindowTarget.From(processName);
        return TryResolveWindowHandle(target, out windowHandle, out windowBounds, out _);
    }

    public bool TryResolveWindowHandle(
        AutomationProcessWindowTarget processTarget,
        out IntPtr windowHandle,
        out AutomationPhysicalRect windowBounds,
        out AutomationProcessWindowTarget resolvedTarget)
    {
        windowHandle = IntPtr.Zero;
        windowBounds = default;
        resolvedTarget = default;

        if (processTarget.IsEmpty)
            return false;

        var normalized = processTarget.ProcessName;

        try
        {
            var processes = ResolveProcesses(processTarget, normalized);
            try
            {
                if (processes.Length == 0)
                    return false;

                var processNamesById = processes.ToDictionary(process => process.Id, process => process.ProcessName);
                var state = new EnumWindowsState(processNamesById);
                var handle = GCHandle.Alloc(state);
                try
                {
                    EnumWindows(EnumWindowsCollect, GCHandle.ToIntPtr(handle));
                }
                finally
                {
                    handle.Free();
                }

                if (state.Candidates.Count == 0)
                {
                    foreach (var process in processes)
                    {
                        if (TryCreateCandidate(process.MainWindowHandle, process.Id, out var candidate))
                        {
                            windowHandle = candidate.Hwnd;
                            windowBounds = candidate.Bounds;
                            resolvedTarget = AutomationProcessWindowTarget.From(process.ProcessName, process.Id);
                            return true;
                        }
                    }

                    return false;
                }

                var selected = state.Candidates.OrderByDescending(candidate => candidate.Area).First();
                windowHandle = selected.Hwnd;
                windowBounds = selected.Bounds;
                var selectedProcessName = processNamesById.GetValueOrDefault(selected.ProcessId, normalized);
                resolvedTarget = AutomationProcessWindowTarget.From(selectedProcessName, selected.ProcessId);
                return windowHandle != IntPtr.Zero;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    private static Process[] ResolveProcesses(AutomationProcessWindowTarget processTarget, string normalizedProcessName)
    {
        if (processTarget.ProcessId > 0)
        {
            try
            {
                var process = Process.GetProcessById(processTarget.ProcessId);
                if (string.IsNullOrWhiteSpace(normalizedProcessName) ||
                    string.Equals(process.ProcessName, normalizedProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return [process];
                }

                process.Dispose();
                return [];
            }
            catch
            {
                return [];
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedProcessName))
            return [];

        return Process.GetProcessesByName(normalizedProcessName);
    }

    private static bool EnumWindowsCollect(IntPtr hWnd, IntPtr lParam)
    {
        var state = (EnumWindowsState)GCHandle.FromIntPtr(lParam).Target!;
        GetWindowThreadProcessId(hWnd, out var pid);
        if (!state.ProcessNamesById.ContainsKey((int)pid))
            return true;

        if (TryCreateCandidate(hWnd, (int)pid, out var candidate))
            state.Candidates.Add(candidate);

        return true;
    }

    private static bool TryCreateCandidate(IntPtr hWnd, int processId, out WindowCandidate candidate)
    {
        candidate = default;
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd) || !IsWindowVisible(hWnd) || IsIconic(hWnd))
            return false;

        if (!GetWindowRect(hWnd, out var rect))
            return false;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 32 || height < 32)
            return false;

        var bounds = new AutomationPhysicalRect(rect.Left, rect.Top, width, height);
        candidate = new WindowCandidate(hWnd, processId, bounds, (long)width * height);
        return true;
    }

    private static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^4].Trim();
        return trimmed;
    }

    private sealed class EnumWindowsState(IReadOnlyDictionary<int, string> processNamesById)
    {
        public IReadOnlyDictionary<int, string> ProcessNamesById { get; } = processNamesById;

        public List<WindowCandidate> Candidates { get; } = [];
    }

    private readonly record struct WindowCandidate(IntPtr Hwnd, int ProcessId, AutomationPhysicalRect Bounds, long Area);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);
}
