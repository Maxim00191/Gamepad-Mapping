using System;
using System.Collections.Generic;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Services;
using GamepadMapping.Tests.Mocks;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class AppStatusMonitorTests
{
    private readonly MockProcessTargetService _mockProcessService;
    private readonly MockElevationHandler _mockElevationHandler;
    private readonly AppStatusMonitor _monitor;

    public AppStatusMonitorTests()
    {
        _mockProcessService = new MockProcessTargetService();
        _mockElevationHandler = new MockElevationHandler();
        // Use a long poll interval to avoid background timer interference during tests
        _monitor = new AppStatusMonitor(_mockProcessService, _mockElevationHandler, TimeSpan.FromHours(1));
    }

    [Fact]
    public void UpdateTarget_NoTarget_ReturnsNoTargetSelected()
    {
        _monitor.UpdateTarget(null, false);
        
        Assert.Equal(AppTargetingState.NoTargetSelected, _monitor.CurrentState);
        Assert.Contains("No target selected", _monitor.CurrentStatusText);
    }

    [Fact]
    public void EvaluateState_ProcessBlockedByUipi_ReturnsBlockedByUipi()
    {
        var target = new ProcessInfo { ProcessId = 1234, ProcessName = "Game.exe" };
        _mockElevationHandler.IsBlockedByUipiFunc = _ => true;

        _monitor.UpdateTarget(target, true);

        Assert.Equal(AppTargetingState.BlockedByUipi, _monitor.CurrentState);
        Assert.Contains("requires admin privileges", _monitor.CurrentStatusText);
    }

    [Fact]
    public void EvaluateState_TargetInForeground_ReturnsConnected()
    {
        var target = new ProcessInfo { ProcessId = 1234, ProcessName = "Game.exe" };
        _mockElevationHandler.IsBlockedByUipiFunc = _ => false;
        _mockProcessService.IsForegroundTargetFunc = _ => true;

        _monitor.UpdateTarget(target, true);

        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);
        Assert.Contains("Connected", _monitor.CurrentStatusText);
    }

    [Fact]
    public void EvaluateState_TargetNotInForeground_ReturnsWaitingForForeground()
    {
        var target = new ProcessInfo { ProcessId = 1234, ProcessName = "Game.exe" };
        _mockElevationHandler.IsBlockedByUipiFunc = _ => false;
        _mockProcessService.IsForegroundTargetFunc = _ => false;

        _monitor.UpdateTarget(target, true);

        Assert.Equal(AppTargetingState.WaitingForForeground, _monitor.CurrentState);
        Assert.Contains("Waiting for target foreground", _monitor.CurrentStatusText);
    }

    [Fact]
    public void EvaluateState_ProcessCrashes_UpdatesStateOnNextEvaluate()
    {
        var target = new ProcessInfo { ProcessId = 1234, ProcessName = "Game.exe" };
        _mockElevationHandler.IsBlockedByUipiFunc = _ => false;
        _mockProcessService.IsForegroundTargetFunc = _ => true;

        _monitor.UpdateTarget(target, true);
        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);

        // Simulate crash: IsForeground returns false because PID is gone
        _mockProcessService.IsForegroundTargetFunc = _ => false;
        _monitor.EvaluateNow();

        Assert.Equal(AppTargetingState.WaitingForForeground, _monitor.CurrentState);
    }
}
