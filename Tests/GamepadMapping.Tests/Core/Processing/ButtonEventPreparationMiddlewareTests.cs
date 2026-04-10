using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services;
using GamepadMapperGUI.Models;
using Moq;


namespace GamepadMapping.Tests.Core.Processing;

public class ButtonEventPreparationMiddlewareTests
{
    private readonly Mock<IAppStatusMonitor> _appStatusMonitorMock;
    private readonly List<string> _mappingStatus = new();

    public ButtonEventPreparationMiddlewareTests()
    {
        _appStatusMonitorMock = new Mock<IAppStatusMonitor>();
    }

    [Fact]
    public void Invoke_WhenNotForeground_SuppressesPressedEvent()
    {
        _appStatusMonitorMock.Setup(m => m.CanSendOutput).Returns(false);
        
        var middleware = CreateMiddleware();
        var context = CreateContext(TriggerMoment.Pressed);
        bool nextCalled = false;

        middleware.Invoke(context, ctx => nextCalled = true);

        Assert.True(context.IsSuppressed);
        Assert.False(nextCalled);
        Assert.Contains(_mappingStatus, s => s.Contains("Suppressed"));
    }

    [Fact]
    public void Invoke_WhenNotForeground_AllowsReleasedEvent()
    {
        // Released events should NOT be suppressed to ensure state cleanup
        _appStatusMonitorMock.Setup(m => m.CanSendOutput).Returns(false);
        
        var middleware = CreateMiddleware();
        var context = CreateContext(TriggerMoment.Released);
        bool nextCalled = false;

        middleware.Invoke(context, ctx => nextCalled = true);

        Assert.False(context.IsSuppressed);
        Assert.True(nextCalled);
    }

    [Fact]
    public void Invoke_WhenPressed_RegistersButton()
    {
        _appStatusMonitorMock.Setup(m => m.CanSendOutput).Returns(true);
        bool registered = false;
        
        var middleware = CreateMiddleware(registerButtonPressed: b => registered = true);
        var context = CreateContext(TriggerMoment.Pressed);

        middleware.Invoke(context, _ => { });

        Assert.True(registered);
    }

    private ButtonEventPreparationMiddleware CreateMiddleware(
        Action<GamepadButtons>? registerButtonPressed = null,
        Action<GamepadButtons>? registerButtonReleased = null)
    {
        return new ButtonEventPreparationMiddleware(
            setLatestInputState: (btns, l, r) => { },
            registerButtonPressed: registerButtonPressed ?? (b => { }),
            registerButtonReleased: registerButtonReleased ?? (b => { }),
            cancelSupersededHoldSessions: (b, btns, m, l, r) => { },
            handleHoldRelease: (b, btns, l, r, ms) => { },
            getReleasedButtonHeldMs: b => 100,
            forceReleaseHeldOutputsForButton: (b, outs) => { },
            collectReleasedOutputsHandledByMappings: (b, btns, m, l, r, ms) => new HashSet<DispatchedOutput>(),
            setLatestActiveButtons: btns => { },
            canDispatchOutput: () => _appStatusMonitorMock.Object.CanSendOutput,
            setMappingStatus: s => _mappingStatus.Add(s)
        );
    }

    private ButtonEventContext CreateContext(TriggerMoment trigger)
    {
        return new ButtonEventContext
        {
            Button = GamepadButtons.A,
            Trigger = trigger,
            ActiveButtons = new[] { GamepadButtons.A },
            MappingsSnapshot = new List<MappingEntry>(),
            LeftTriggerValue = 0,
            RightTriggerValue = 0
        };
    }
}


