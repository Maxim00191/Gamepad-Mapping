using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

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

