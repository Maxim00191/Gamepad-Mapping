using System;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;

namespace GamepadMapperGUI.Interfaces.Services;

public interface IAppStatusMonitor : IDisposable
{
    event EventHandler<AppStatusChangedEventArgs>? StatusChanged;
    AppTargetingState CurrentState { get; }
    string CurrentStatusText { get; }
    bool CanSendOutput { get; }
    void UpdateTarget(ProcessInfo? selectedTargetProcess, bool isProcessTargetingEnabled);
    void UpdateGracePeriod(int gracePeriodMs);
    /// <summary>Re-evaluates foreground/targeting and updates status when it changes. Returns whether mapped output is allowed right now.</summary>
    bool EvaluateNow();
}
