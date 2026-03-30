using System.Collections.Generic;
using GamepadMapperGUI.Models;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IProcessTargetService
{
    List<ProcessInfo> GetRecentWindowedProcesses();
    int GetForegroundProcessId();
    bool IsForeground(int processId);
    bool IsForeground(string processName);
    string GetForegroundWindowTitle();
    bool IsForeground(ProcessInfo target);
    bool IsCurrentProcessElevated();
    bool IsProcessElevated(int processId);
}
