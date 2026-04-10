using System;
using System.Diagnostics;
using System.Threading;
using Gamepad_Mapping;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Utils;
using GamepadMapperGUI.Models.State;

namespace GamepadMapperGUI.Services.Infrastructure;

public sealed class AppStatusMonitor : IAppStatusMonitor
{
    private readonly IProcessTargetService _processTargetService;
    private readonly IElevationHandler _elevationHandler;
    private readonly Timer _timer;
    private readonly object _sync = new();

    private ProcessInfo? _selectedTargetProcess;
    private bool _isProcessTargetingEnabled;
    private int _focusGracePeriodMs = 500;
    private bool _disposed;
    private long _lastForegroundMatchTimestamp = 0;

    public AppStatusMonitor(
        IProcessTargetService processTargetService,
        IElevationHandler elevationHandler,
        TimeSpan? pollInterval = null,
        int initialGracePeriodMs = 500)
    {
        _processTargetService = processTargetService;
        _elevationHandler = elevationHandler;
        _focusGracePeriodMs = initialGracePeriodMs;
        CurrentState = AppTargetingState.NoTargetSelected;
        CurrentStatusText = "No target selected - output suppressed";

        var interval = pollInterval ?? TimeSpan.FromSeconds(1);
        _timer = new Timer(_ => EvaluateNow(), null, interval, interval);
    }

    public event EventHandler<AppStatusChangedEventArgs>? StatusChanged;

    public AppTargetingState CurrentState { get; private set; }

    public string CurrentStatusText { get; private set; }

    public bool CanSendOutput
    {
        get
        {
            lock (_sync)
                return CurrentState == AppTargetingState.Connected;
        }
    }

    public void UpdateTarget(ProcessInfo? selectedTargetProcess, bool isProcessTargetingEnabled)
    {
        lock (_sync)
        {
            _selectedTargetProcess = selectedTargetProcess;
            _isProcessTargetingEnabled = isProcessTargetingEnabled;
        }

        EvaluateNow();
    }

    public void UpdateGracePeriod(int gracePeriodMs)
    {
        lock (_sync)
        {
            _focusGracePeriodMs = gracePeriodMs;
        }
    }

    public bool EvaluateNow()
    {
        try
        {
            AppTargetingState newState;
            string newStatusText;
            bool shouldNotify;
            bool canSend;

            lock (_sync)
            {
                (newState, newStatusText) = EvaluateStateInternal();
                shouldNotify = newState != CurrentState || !string.Equals(newStatusText, CurrentStatusText, StringComparison.Ordinal);
                if (shouldNotify)
                {
                    CurrentState = newState;
                    CurrentStatusText = newStatusText;
                }
                canSend = CurrentState == AppTargetingState.Connected;
            }

            if (shouldNotify)
                StatusChanged?.Invoke(this, new AppStatusChangedEventArgs(newState, newStatusText));

            return canSend;
        }
        catch (Exception ex)
        {
            App.Logger.Error("Error during AppStatusMonitor.EvaluateNow", ex);
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        _timer.Dispose();
    }

    private (AppTargetingState State, string StatusText) EvaluateStateInternal()
    {
        if (!_isProcessTargetingEnabled || _selectedTargetProcess is null)
        {
            _lastForegroundMatchTimestamp = 0;
            return (AppTargetingState.NoTargetSelected, "No target selected - output suppressed");
        }

        var uipiTarget = ResolveProcessForUipi(_selectedTargetProcess);
        if (uipiTarget is not null && _elevationHandler.IsBlockedByUipi(uipiTarget))
        {
            _lastForegroundMatchTimestamp = 0;
            return (
                AppTargetingState.BlockedByUipi,
                $"Target requires admin privileges: {uipiTarget.ProcessName} (PID {uipiTarget.ProcessId})");
        }

        var now = Environment.TickCount64;
        var isForegroundMatch = _processTargetService.IsForeground(_selectedTargetProcess);
        
        if (isForegroundMatch)
        {
            _lastForegroundMatchTimestamp = now;
            var pid = DisplayPidForStatus(_selectedTargetProcess);
            return (AppTargetingState.Connected, $"Connected: {_selectedTargetProcess.ProcessName} (PID {pid})");
        }

        // If we were connected recently, allow a grace period before cutting off input.
        if (_lastForegroundMatchTimestamp > 0 && (now - _lastForegroundMatchTimestamp) < _focusGracePeriodMs)
        {
            var pid = DisplayPidForStatus(_selectedTargetProcess);
            return (AppTargetingState.Connected, $"Connected (Grace Period): {_selectedTargetProcess.ProcessName} (PID {pid})");
        }

        return (
            AppTargetingState.WaitingForForeground,
            $"Waiting for target foreground: {_selectedTargetProcess.ProcessName} (PID {DisplayPidForStatus(_selectedTargetProcess)})");
    }

    private int DisplayPidForStatus(ProcessInfo selected)
    {
        if (selected.ProcessId > 0)
            return selected.ProcessId;

        var fg = _processTargetService.GetForegroundProcessId();
        if (fg <= 0)
            return 0;

        try
        {
            using var p = Process.GetProcessById(fg);
            if (string.Equals(p.ProcessName, selected.ProcessName, StringComparison.OrdinalIgnoreCase))
                return fg;
        }
        catch
        {
            // Ignore.
        }

        return 0;
    }

    /// <summary>When only a declared name is known (PID 0), use the foreground process id if its name matches — needed for correct UIPI checks.</summary>
    private ProcessInfo? ResolveProcessForUipi(ProcessInfo selected)
    {
        if (selected.ProcessId > 0)
            return selected;

        var fg = _processTargetService.GetForegroundProcessId();
        if (fg <= 0)
            return selected;

        try
        {
            using var p = Process.GetProcessById(fg);
            if (string.Equals(p.ProcessName, selected.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessInfo
                {
                    ProcessId = fg,
                    ProcessName = selected.ProcessName,
                    MainWindowTitle = selected.MainWindowTitle
                };
            }
        }
        catch
        {
            // Best-effort: fall back to declared ProcessInfo.
        }

        return selected;
    }
}


