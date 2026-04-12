using System.Numerics;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Core;
using GamepadMapperGUI.Models;
using GamepadMapping.Tests.Mocks;
using Moq;
using Vortice.XInput;
using GamepadButtons = GamepadMapperGUI.Models.GamepadButtons;


namespace GamepadMapping.Tests.Core.Input;

public class GamepadReaderTests
{
    private readonly Mock<IGamepadSource> _mockSource;
    private readonly GamepadReader _gamepadReader;
    private readonly List<InputFrame> _capturedInputFrames;

    public GamepadReaderTests()
    {
        _mockSource = new Mock<IGamepadSource>();
        _capturedInputFrames = new List<InputFrame>();
        _gamepadReader = new GamepadReader(_mockSource.Object);
        _gamepadReader.OnInputFrame += frame => _capturedInputFrames.Add(frame);
    }

    [Fact]
    public async Task PollingLoop_HandlesExceptionsGracefully()
    {
        // Arrange
        int callCount = 0;
        _mockSource
            .Setup(x => x.TryGetFrame(out It.Ref<InputFrame>.IsAny))
            .Callback(new TryGetFrameCallback((out InputFrame f) => 
            {
                callCount++;
                if (callCount == 2)
                {
                    throw new Exception("Simulated source exception");
                }
                f = new InputFrame(GamepadButtons.None, Vector2.Zero, Vector2.Zero, 0, 0, true, 0);
            }))
            .Returns(true);

        // Act
        _gamepadReader.Start();
        await Task.Delay(200); 
        _gamepadReader.Stop();

        // Assert
        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task PollingLoop_EmitsDisconnectedFrame_WhenGamepadDisconnects()
    {
        // Arrange
        int callCount = 0;
        _mockSource
            .Setup(x => x.TryGetFrame(out It.Ref<InputFrame>.IsAny))
            .Callback(new TryGetFrameCallback((out InputFrame f) => 
            {
                callCount++;
                if (callCount == 1 || callCount == 4)
                {
                    f = new InputFrame(GamepadButtons.None, Vector2.Zero, Vector2.Zero, 0, 0, true, 0);
                }
                else
                {
                    f = InputFrame.Disconnected(0);
                }
            }))
            .Returns(() => callCount == 1 || callCount == 4);

        // Act
        _gamepadReader.Start();
        await Task.Delay(200); 
        _gamepadReader.Stop();

        // Assert
        Assert.NotEmpty(_capturedInputFrames);
        Assert.Contains(_capturedInputFrames, f => !f.IsConnected);
    }

    [Fact]
    public async Task PollingLoop_UpdatesPreviousStateCorrectly()
    {
        // Arrange
        _mockSource
            .Setup(x => x.TryGetFrame(out It.Ref<InputFrame>.IsAny))
            .Callback(new TryGetFrameCallback((out InputFrame f) => 
            {
                f = new InputFrame(GamepadButtons.None, Vector2.Zero, Vector2.Zero, 0, 0, true, 0);
            }))
            .Returns(true);

        // Act
        _gamepadReader.Start();
        await Task.Delay(200); 
        _gamepadReader.Stop();

        // Assert
        Assert.True(_capturedInputFrames.Count(f => f.IsConnected) >= 1);
    }

    private delegate void TryGetFrameCallback(out InputFrame frame);
}


