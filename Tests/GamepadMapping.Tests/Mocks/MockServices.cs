using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;

namespace GamepadMapping.Tests.Mocks;

public class MockElevationHandler : IElevationHandler
{
    public Func<ProcessInfo, bool> IsBlockedByUipiFunc { get; set; } = _ => false;
    public Func<ProcessInfo, bool> CheckAndPromptElevationFunc { get; set; } = _ => false;

    public bool IsBlockedByUipi(ProcessInfo target) => IsBlockedByUipiFunc(target);
    public bool CheckAndPromptElevation(ProcessInfo target) => CheckAndPromptElevationFunc(target);
}

public class MockProcessTargetService : IProcessTargetService
{
    public Func<string?, ProcessInfo> CreateTargetFromDeclaredProcessNameFunc { get; set; } = _ => new ProcessInfo();
    public Func<List<ProcessInfo>> GetRecentWindowedProcessesFunc { get; set; } = () => new List<ProcessInfo>();
    public Func<int> GetForegroundProcessIdFunc { get; set; } = () => 0;
    public Func<int, bool> IsForegroundPidFunc { get; set; } = _ => false;
    public Func<string, bool> IsForegroundNameFunc { get; set; } = _ => false;
    public Func<string> GetForegroundWindowTitleFunc { get; set; } = () => string.Empty;
    public Func<ProcessInfo, bool> IsForegroundTargetFunc { get; set; } = _ => false;
    public Func<bool> IsCurrentProcessElevatedFunc { get; set; } = () => false;
    public Func<int, bool> IsProcessElevatedFunc { get; set; } = _ => false;

    public ProcessInfo CreateTargetFromDeclaredProcessName(string? rawName) => CreateTargetFromDeclaredProcessNameFunc(rawName);
    public List<ProcessInfo> GetRecentWindowedProcesses() => GetRecentWindowedProcessesFunc();
    public int GetForegroundProcessId() => GetForegroundProcessIdFunc();
    public bool IsForeground(int processId) => IsForegroundPidFunc(processId);
    public bool IsForeground(string processName) => IsForegroundNameFunc(processName);
    public string GetForegroundWindowTitle() => GetForegroundWindowTitleFunc();
    public bool IsForeground(ProcessInfo target) => IsForegroundTargetFunc(target);
    public bool IsCurrentProcessElevated() => IsCurrentProcessElevatedFunc();
    public bool IsProcessElevated(int processId) => IsProcessElevatedFunc(processId);
}

