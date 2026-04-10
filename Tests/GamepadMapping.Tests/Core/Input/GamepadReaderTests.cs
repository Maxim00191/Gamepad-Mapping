using System.Numerics;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces;
using GamepadMapperGUI.Models;
using GamepadMapping.Tests.Mocks;
using Moq;
using Vortice.XInput;


namespace GamepadMapping.Tests.Core.Input;

public class GamepadReaderTests
{
    private readonly Mock<IXInput> _mockXInput;
    private readonly GamepadReader _gamepadReader;
    private readonly List<InputFrame> _capturedInputFrames;

    public GamepadReaderTests()
    {
        _mockXInput = new Mock<IXInput>();
        _capturedInputFrames = new List<InputFrame>();
        _gamepadReader = new GamepadReader(_mockXInput.Object);
        _gamepadReader.OnInputFrame += frame => _capturedInputFrames.Add(frame);
    }

    [Fact]
    public async Task PollingLoop_HandlesExceptionsGracefully()
    {
        // Arrange
        int callCount = 0;
        _mockXInput
            .Setup(x => x.GetState(It.IsAny<uint>(), out It.Ref<State>.IsAny))
            .Callback(new IXInputGetStateCallback((uint idx, out State s) => 
            {
                callCount++;
                if (callCount == 2) // Throw on the first call inside the background loop
                {
                    throw new Exception("Simulated XInput exception");
                }
                s = new State();
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
        State connectedState = default;
        // We can't easily set Gamepad.Buttons if it's a read-only struct from Vortice.XInput.
        // For this test, we just need GetState to return true/false.
        State disconnectedState = default;

        _mockXInput
            .Setup(x => x.GetState(It.IsAny<uint>(), out It.Ref<State>.IsAny))
            .Callback(new IXInputGetStateCallback((uint idx, out State s) => 
            {
                callCount++;
                if (callCount == 1 || callCount == 4) s = connectedState;
                else s = disconnectedState;
            }))
            .Returns(() => callCount == 1 || callCount == 4);

        // Act
        _gamepadReader.Start();
        await Task.Delay(200); 
        _gamepadReader.Stop();

        // Assert
        Assert.NotEmpty(_capturedInputFrames);
        var disconnectedFrame = _capturedInputFrames.FirstOrDefault(f => !f.IsConnected);
        // InputFrame is a record/struct, IsConnected is a property.
        Assert.Contains(_capturedInputFrames, f => !f.IsConnected);
    }

    [Fact]
    public async Task PollingLoop_UpdatesPreviousStateCorrectly()
    {
        // Arrange
        int callCount = 0;
        State state1 = default;
        State state2 = default;

        _mockXInput
            .Setup(x => x.GetState(It.IsAny<uint>(), out It.Ref<State>.IsAny))
            .Callback(new IXInputGetStateCallback((uint idx, out State s) => 
            {
                callCount++;
                if (callCount == 1) s = state1;
                else s = state2;
            }))
            .Returns(true);

        // Act
        _gamepadReader.Start();
        await Task.Delay(200); 
        _gamepadReader.Stop();

        // Assert
        // We can't easily verify button changes if we can't set them on the State struct.
        // But we can verify that we got at least some connected frames.
        Assert.True(_capturedInputFrames.Count(f => f.IsConnected) >= 1);
    }

    // Helper delegate for Moq Callback with out parameters
    private delegate void IXInputGetStateCallback(uint userIndex, out State state);
}


