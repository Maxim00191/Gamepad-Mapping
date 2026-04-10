using System;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using GamepadMapperGUI.Models.State;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Moq;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class AppStatusMonitorTests
{
    private readonly Mock<IProcessTargetService> _processTargetMock;
    private readonly Mock<IElevationHandler> _elevationHandlerMock;
    private readonly AppStatusMonitor _monitor;

    public AppStatusMonitorTests()
    {
        _processTargetMock = new Mock<IProcessTargetService>();
        _elevationHandlerMock = new Mock<IElevationHandler>();
        // Use a long poll interval to prevent timer interference during tests
        _monitor = new AppStatusMonitor(_processTargetMock.Object, _elevationHandlerMock.Object, TimeSpan.FromHours(1));
    }

    [Fact]
    public void UpdateTarget_NoTarget_SetsNoTargetSelectedState()
    {
        // Act
        _monitor.UpdateTarget(null, true);

        // Assert
        Assert.Equal(AppTargetingState.NoTargetSelected, _monitor.CurrentState);
    }

    [Fact]
    public void EvaluateNow_TargetNotForeground_SetsWaitingForForegroundState()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(false);
        _elevationHandlerMock.Setup(x => x.IsBlockedByUipi(target)).Returns(false);

        // Act
        _monitor.UpdateTarget(target, true);

        // Assert
        Assert.Equal(AppTargetingState.WaitingForForeground, _monitor.CurrentState);
    }

    [Fact]
    public void EvaluateNow_TargetIsForeground_SetsConnectedState()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(true);
        _elevationHandlerMock.Setup(x => x.IsBlockedByUipi(target)).Returns(false);

        // Act
        _monitor.UpdateTarget(target, true);

        // Assert
        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);
    }

    [Fact]
    public void EvaluateNow_TargetBlockedByUipi_SetsBlockedByUipiState()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _elevationHandlerMock.Setup(x => x.IsBlockedByUipi(It.IsAny<ProcessInfo>())).Returns(true);

        // Act
        _monitor.UpdateTarget(target, true);

        // Assert
        Assert.Equal(AppTargetingState.BlockedByUipi, _monitor.CurrentState);
    }

    [Fact]
    public void StatusChanged_FiresWhenStateChanges()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(true);
        bool eventFired = false;
        _monitor.StatusChanged += (s, e) => eventFired = true;

        // Act
        _monitor.UpdateTarget(target, true);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void EvaluateNow_FocusLostWithinGracePeriod_MaintainsConnectedState()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _elevationHandlerMock.Setup(x => x.IsBlockedByUipi(It.IsAny<ProcessInfo>())).Returns(false);
        
        // Initial state: foreground
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(true);
        _monitor.UpdateTarget(target, true);
        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);

        // Act: Focus lost
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(false);
        _monitor.EvaluateNow();

        // Assert: Should still be connected because of grace period (default 500ms)
        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);
        Assert.Contains("Grace Period", _monitor.CurrentStatusText);
    }

    [Fact]
    public async Task EvaluateNow_FocusLostBeyondGracePeriod_SetsWaitingForForegroundState()
    {
        // Arrange
        var target = new ProcessInfo { ProcessId = 123, ProcessName = "Game" };
        _elevationHandlerMock.Setup(x => x.IsBlockedByUipi(It.IsAny<ProcessInfo>())).Returns(false);
        
        // Use a very short grace period for testing
        _monitor.UpdateGracePeriod(50); 
        
        // Initial state: foreground
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(true);
        _monitor.UpdateTarget(target, true);
        Assert.Equal(AppTargetingState.Connected, _monitor.CurrentState);

        // Wait beyond grace period
        await Task.Delay(100);

        // Act: Focus lost
        _processTargetMock.Setup(x => x.IsForeground(target)).Returns(false);
        _monitor.EvaluateNow();

        // Assert: Should now be waiting for foreground
        Assert.Equal(AppTargetingState.WaitingForForeground, _monitor.CurrentState);
    }
}


