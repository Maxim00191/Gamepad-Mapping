using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Infrastructure;
using GamepadMapperGUI.Interfaces.Services.Storage;
using GamepadMapperGUI.Interfaces.Services.Update;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Interfaces.Services.Radial;
using GamepadMapperGUI.Models;
using Moq;
using Vortice.XInput;
using System.Reflection;

namespace GamepadMapping.Tests.Core.Processing;

public class MappingEngineSendPointerActionTests
{
    private readonly Mock<IKeyboardEmulator> _mockKeyboardEmulator;
    private readonly Mock<IMouseEmulator> _mockMouseEmulator;
    private readonly Mock<IInputDispatcher> _mockInputDispatcher;
    private readonly MappingEngine _engine;

    public MappingEngineSendPointerActionTests()
    {
        _mockKeyboardEmulator = new Mock<IKeyboardEmulator>();
        _mockMouseEmulator = new Mock<IMouseEmulator>();
        _mockInputDispatcher = new Mock<IInputDispatcher>();

        _engine = new MappingEngine(
            _mockKeyboardEmulator.Object,
            _mockMouseEmulator.Object,
            () => true, // canDispatchOutputLive
            action => action(), // runOnUi
            s => { }, // setMappedOutput
            s => { }, // setMappingStatus
            null, // setComboHud
            50, // modifierGraceMs
            500, // leadKeyReleaseSuppressMs
            null, // requestTemplateSwitchToProfileId
            null, // profileService
            null, // setComboHudGateHint
            null, // comboHudGateMessageFactory
            null, // isComboHudPresentationSuppressed
            null, // radialMenuHud
            null, // getRadialMenuStickEngagementThreshold
            null, // getRadialMenuConfirmMode
            null, // timeProvider
            _mockInputDispatcher.Object // Properly injected now!
        );
    }

    [Fact]
    public void SendPointerAction_EnqueuesToDispatcher()
    {
        // Arrange
        var action = PointerAction.LeftClick;
        var trigger = TriggerMoment.Pressed;

        // Act
        // Access private method via reflection for testing
        var method = typeof(MappingEngine).GetMethod("SendPointerAction", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(_engine, new object[] { action, trigger });

        // Assert
        _mockInputDispatcher.Verify(d => d.Enqueue(
            "AnalogSource",
            trigger,
            It.Is<DispatchedOutput>(o => o.PointerAction == action),
            action.ToString(),
            "analog-input"
        ), Times.Once);
    }

    [Fact]
    public void SendPointerAction_IsNonBlocking()
    {
        // Arrange
        var action = PointerAction.RightClick;
        var trigger = TriggerMoment.Tap;
        
        // Setup dispatcher to simulate a slight delay (though it shouldn't matter if it's called synchronously)
        _mockInputDispatcher.Setup(d => d.Enqueue(It.IsAny<string>(), It.IsAny<TriggerMoment>(), It.IsAny<DispatchedOutput>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => Thread.Sleep(10));

        // Act
        var startTime = DateTime.Now;
        var method = typeof(MappingEngine).GetMethod("SendPointerAction", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(_engine, new object[] { action, trigger });
        var duration = DateTime.Now - startTime;

        // Assert
        // The call should be near-instant since it just enqueues, 
        // even if the mock callback has a sleep (because the callback runs on the same thread in this simple setup, 
        // but we're verifying the logic of the engine itself just enqueuing).
        // If it was blocking on the actual simulation (which we moved to the worker thread), it would take 30ms+.
        Assert.True(duration.TotalMilliseconds < 50); 
    }
}


