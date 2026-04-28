using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Moq;
using Vortice.XInput;
using GamepadButtons = GamepadMapperGUI.Models.GamepadButtons;

namespace GamepadMapping.Tests.Core.Input;

public sealed class PlayStationCompatibleXInputSourceTests
{
    [Fact]
    public void TryGetFrame_WhenNativeStateMissing_FallsBackToXInput()
    {
        var providerMock = new Mock<IPlayStationInputProvider>();
        var unusedState = default(PlayStationInputState);
        providerMock
            .Setup(p => p.TryGetState(out unusedState))
            .Returns(false);

        var xInputMock = new Mock<IXInput>();
        var xState = new State
        {
            Gamepad = new Gamepad
            {
                Buttons = Vortice.XInput.GamepadButtons.B,
                LeftTrigger = 128,
                RightTrigger = 200
            }
        };

        xInputMock
            .Setup(x => x.GetState(0u, out xState))
            .Returns(true);

        var source = new PlayStationCompatibleXInputSource(providerMock.Object, xInputMock.Object);

        var ok = source.TryGetFrame(out var frame);

        Assert.True(ok);
        Assert.True(frame.IsConnected);
        Assert.Equal(GamepadButtons.B, frame.Buttons);
        Assert.Null(frame.PlayStationState);
    }
}
