using System;
using System.Threading;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;

namespace GamepadMapperGUI.Services;

public enum AppTargetingState
{
    NoTargetSelected = 0,
    BlockedByUipi = 1,
    WaitingForForeground = 2,
    Connected = 3
}

public sealed class AppStatusChangedEventArgs : EventArgs
{
    public AppStatusChangedEventArgs(AppTargetingState state, string statusText)
    {
        State = state;
        StatusText = statusText;
    }

    public AppTargetingState State { get; }

    public string StatusText { get; }
}

public sealed class AppStatusMonitor : IAppStatusMonitor
{
    private readonly IProcessTargetService _processTargetService;
    private readonly IElevationHandler _elevationHandler;
    private readonly Timer _timer;
    private readonly object _sync = new();

    private ProcessInfo? _selectedTargetProcess;
    private bool _isProcessTargetingEnabled;
    private bool _disposed;

    public AppStatusMonitor(
        IProcessTargetService processTargetService,
        IElevationHandler elevationHandler,
        TimeSpan? pollInterval = null)
    {
        _processTargetService = processTargetService;
        _elevationHandler = elevationHandler;
        CurrentState = AppTargetingState.NoTargetSelected;
        CurrentStatusText = "No target selected - output suppressed";

        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        _timer = new Timer(_ => EvaluateNow(), null, interval, interval);
    }

    public event EventHandler<AppStatusChangedEventArgs>? StatusChanged;

    public AppTargetingState CurrentState { get; private set; }

    public string CurrentStatusText { get; private set; }

    public bool CanSendOutput => CurrentState == AppTargetingState.Connected;

    public void UpdateTarget(ProcessInfo? selectedTargetProcess, bool isProcessTargetingEnabled)
    {
        lock (_sync)
        {
            _selectedTargetProcess = selectedTargetProcess;
            _isProcessTargetingEnabled = isProcessTargetingEnabled;
        }

        EvaluateNow();
    }

    public void EvaluateNow()
    {
        ProcessInfo? selectedTargetProcess;
        bool isProcessTargetingEnabled;
        lock (_sync)
        {
            selectedTargetProcess = _selectedTargetProcess;
            isProcessTargetingEnabled = _isProcessTargetingEnabled;
        }

        var (state, statusText) = EvaluateState(selectedTargetProcess, isProcessTargetingEnabled);
        if (state == CurrentState && string.Equals(statusText, CurrentStatusText, StringComparison.Ordinal))
            return;

        CurrentState = state;
        CurrentStatusText = statusText;
        StatusChanged?.Invoke(this, new AppStatusChangedEventArgs(state, statusText));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Dispose();
    }

    private (AppTargetingState State, string StatusText) EvaluateState(
        ProcessInfo? selectedTargetProcess,
        bool isProcessTargetingEnabled)
    {
        if (!isProcessTargetingEnabled || selectedTargetProcess is null)
            return (AppTargetingState.NoTargetSelected, "No target selected - output suppressed");

        if (_elevationHandler.IsBlockedByUipi(selectedTargetProcess))
        {
            return (
                AppTargetingState.BlockedByUipi,
                $"Target requires admin privileges: {selectedTargetProcess.ProcessName} (PID {selectedTargetProcess.ProcessId})");
        }

        var isForegroundMatch = _processTargetService.IsForeground(selectedTargetProcess);
        if (isForegroundMatch)
            return (AppTargetingState.Connected, $"Connected: {selectedTargetProcess.ProcessName} (PID {selectedTargetProcess.ProcessId})");

        return (
            AppTargetingState.WaitingForForeground,
            $"Waiting for target foreground: {selectedTargetProcess.ProcessName} (PID {selectedTargetProcess.ProcessId})");
    }
}
