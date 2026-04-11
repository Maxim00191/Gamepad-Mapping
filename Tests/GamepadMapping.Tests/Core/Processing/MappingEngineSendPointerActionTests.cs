using System.Reflection;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;

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
            () => true,
            action => action(),
            _ => { },
            _ => { },
            null,
            50,
            500,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockInputDispatcher.Object);
    }

    [Fact]
    public void QueueOutputDispatch_EnqueuesToDispatcher()
    {
        var action = PointerAction.LeftClick;
        var trigger = TriggerMoment.Pressed;
        var output = new DispatchedOutput(null, action);

        var method = typeof(MappingEngine).GetMethod(
            "QueueOutputDispatch",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(
            _engine,
            ["AnalogSource", trigger, output, action.ToString(), "analog-input"]);

        _mockInputDispatcher.Verify(
            d => d.Enqueue(
                "AnalogSource",
                trigger,
                output,
                action.ToString(),
                "analog-input"),
            Times.Once());
    }

    [Fact]
    public void QueueOutputDispatch_IsNonBlocking()
    {
        var action = PointerAction.RightClick;
        var trigger = TriggerMoment.Tap;
        var output = new DispatchedOutput(null, action);

        _mockInputDispatcher
            .Setup(d => d.Enqueue(
                It.IsAny<string>(),
                It.IsAny<TriggerMoment>(),
                It.IsAny<DispatchedOutput>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback(() => Thread.Sleep(10));

        var method = typeof(MappingEngine).GetMethod(
            "QueueOutputDispatch",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var startTime = DateTime.Now;
        method!.Invoke(
            _engine,
            ["AnalogSource", trigger, output, action.ToString(), "analog-input"]);
        var duration = DateTime.Now - startTime;

        Assert.True(duration.TotalMilliseconds < 50);
    }
}
