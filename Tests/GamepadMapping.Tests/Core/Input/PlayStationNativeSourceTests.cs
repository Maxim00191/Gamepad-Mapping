using System.Numerics;
using GamepadMapperGUI.Core.Input;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using Moq;
using GamepadButtons = GamepadMapperGUI.Models.GamepadButtons;

namespace GamepadMapping.Tests.Core.Input;

public sealed class PlayStationNativeSourceTests
{
    [Fact]
    public void TryGetFrame_WhenNativeStateAvailable_ReturnsNativeFrameWithPlayStationState()
    {
        var nativeState = new PlayStationInputState(
            Buttons: GamepadButtons.A,
            LeftThumbstick: new Vector2(0.1f, 0.2f),
            RightThumbstick: new Vector2(0.3f, 0.4f),
            LeftTrigger: 0.5f,
            RightTrigger: 0.6f,
            Gyroscope: new Vector3(1, 2, 3),
            IsTouchpadPressed: true,
            PrimaryTouch: new PlayStationTouchPoint(true, 1, 0.2f, 0.3f),
            SecondaryTouch: new PlayStationTouchPoint(false, 0, 0f, 0f),
            TimestampMs: 12345);

        var providerMock = new Mock<IPlayStationInputProvider>();
        providerMock
            .Setup(p => p.TryGetState(out nativeState))
            .Returns(true);

        var source = new PlayStationNativeSource(providerMock.Object);

        var ok = source.TryGetFrame(out var frame);

        Assert.True(ok);
        Assert.True(frame.IsConnected);
        Assert.Equal(nativeState.TimestampMs, frame.TimestampMs);
        Assert.Equal(nativeState.Buttons, frame.Buttons);
        Assert.NotNull(frame.PlayStationState);
        Assert.Equal(nativeState.Gyroscope, frame.PlayStationState!.Value.Gyroscope);
    }

    [Fact]
    public void TryGetFrame_WhenNativeStateMissing_ReturnsDisconnectedWithoutTouchingXInput()
    {
        var providerMock = new Mock<IPlayStationInputProvider>();
        var unusedState = default(PlayStationInputState);
        providerMock
            .Setup(p => p.TryGetState(out unusedState))
            .Returns(false);

        var source = new PlayStationNativeSource(providerMock.Object);

        var ok = source.TryGetFrame(out var frame);

        Assert.False(ok);
        Assert.False(frame.IsConnected);
        Assert.Equal(GamepadButtons.None, frame.Buttons);
        Assert.Null(frame.PlayStationState);
    }
}
