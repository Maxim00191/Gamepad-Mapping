using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

public interface IProcessTargetService
{
    /// <summary>Builds a <see cref="ProcessInfo"/> from a user-declared name (optional .exe); resolves <see cref="ProcessInfo.ProcessId"/> when a matching process is running.</summary>
    ProcessInfo CreateTargetFromDeclaredProcessName(string? rawName);

    List<ProcessInfo> GetRecentWindowedProcesses();
    int GetForegroundProcessId();
    bool IsForeground(int processId);
    bool IsForeground(string processName);
    string GetForegroundWindowTitle();
    bool IsForeground(ProcessInfo target);
    bool IsCurrentProcessElevated();
    bool IsProcessElevated(int processId);
}

