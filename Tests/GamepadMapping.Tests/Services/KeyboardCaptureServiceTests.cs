using System.Windows.Input;
using GamepadMapperGUI.Services.Infrastructure;
using GamepadMapperGUI.Services.Storage;
using GamepadMapperGUI.Services.Update;
using GamepadMapperGUI.Services.Input;
using GamepadMapperGUI.Services.Radial;
using Xunit;

namespace GamepadMapping.Tests.Services;

public class KeyboardCaptureServiceTests
{
    private readonly KeyboardCaptureService _service;

    public KeyboardCaptureServiceTests()
    {
        _service = new KeyboardCaptureService();
    }

    [Fact]
    public void BeginCapture_SetsIsRecordingToTrue()
    {
        // Act
        _service.BeginCapture("Press a key", _ => { });

        // Assert
        Assert.True(_service.IsRecordingKeyboardKey);
        Assert.Equal("Press a key", _service.KeyboardKeyCapturePrompt);
    }

    [Fact]
    public void TryCaptureKeyboardKey_CapturesNormalKey()
    {
        // Arrange
        Key capturedKey = Key.None;
        _service.BeginCapture("Press a key", k => capturedKey = k);

        // Act
        bool result = _service.TryCaptureKeyboardKey(Key.A);

        // Assert
        Assert.True(result);
        Assert.Equal(Key.A, capturedKey);
        Assert.False(_service.IsRecordingKeyboardKey);
    }

    [Fact]
    public void TryCaptureKeyboardKey_CapturesSystemKey()
    {
        // Arrange
        Key capturedKey = Key.None;
        _service.BeginCapture("Press a key", k => capturedKey = k);

        // Act
        bool result = _service.TryCaptureKeyboardKey(Key.System, Key.LeftAlt);

        // Assert
        Assert.True(result);
        Assert.Equal(Key.LeftAlt, capturedKey);
    }

    [Fact]
    public void TryCaptureKeyboardKey_IgnoresNoneKey()
    {
        // Arrange
        _service.BeginCapture("Press a key", _ => { });

        // Act
        bool result = _service.TryCaptureKeyboardKey(Key.None);

        // Assert
        Assert.False(result);
        Assert.True(_service.IsRecordingKeyboardKey);
    }

    [Fact]
    public void CancelCapture_ResetsState()
    {
        // Arrange
        _service.BeginCapture("Press a key", _ => { });

        // Act
        _service.CancelCapture();

        // Assert
        Assert.False(_service.IsRecordingKeyboardKey);
        Assert.Empty(_service.KeyboardKeyCapturePrompt);
    }
}

