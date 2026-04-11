using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;

namespace GamepadMapping.Tests.Core.Processing;

public sealed class AnalogMappingProcessorTests
{
    private static MappingEntry StickDir(GamepadBindingType stick, string fromValue, string key) =>
        new()
        {
            From = new GamepadBinding { Type = stick, Value = fromValue },
            KeyboardKey = key,
            Trigger = TriggerMoment.Pressed
        };

    [Fact]
    public void ProcessThumbstick_WasdOnly_DoesNotInvokeMouseMoveBy_ForNoise()
    {
        var mappings = new List<MappingEntry>
        {
            StickDir(GamepadBindingType.LeftThumbstick, "UP", "W"),
            StickDir(GamepadBindingType.LeftThumbstick, "DOWN", "S"),
            StickDir(GamepadBindingType.LeftThumbstick, "LEFT", "A"),
            StickDir(GamepadBindingType.LeftThumbstick, "RIGHT", "D")
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { });

        sut.ProcessThumbstick(GamepadBindingType.LeftThumbstick, new Vector2(1f, 0f), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()),
            Times.Never);
    }

    [Fact]
    public void ProcessThumbstick_WasdOnly_OnRightStick_DoesNotInvokeMouseMoveBy_ForNoise()
    {
        var mappings = new List<MappingEntry>
        {
            StickDir(GamepadBindingType.RightThumbstick, "UP", "W"),
            StickDir(GamepadBindingType.RightThumbstick, "DOWN", "S"),
            StickDir(GamepadBindingType.RightThumbstick, "LEFT", "A"),
            StickDir(GamepadBindingType.RightThumbstick, "RIGHT", "D")
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { });

        sut.ProcessThumbstick(GamepadBindingType.RightThumbstick, new Vector2(0f, 1f), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()),
            Times.Never);
    }

    [Fact]
    public void ProcessThumbstick_MouseLookOnSameStick_InvokesMoveBy()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "X" },
                KeyboardKey = "mousex",
                Trigger = TriggerMoment.Pressed
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { });

        sut.ProcessThumbstick(GamepadBindingType.LeftThumbstick, new Vector2(1f, 0f), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ProcessThumbstick_MouseLookAndDirectionalKey_OnLeftStick_InvokesPixelMoveBy_AndKeyboard()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.LeftThumbstick, Value = "X" },
                KeyboardKey = "mousex",
                Trigger = TriggerMoment.Pressed
            },
            StickDir(GamepadBindingType.LeftThumbstick, "RIGHT", "D")
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { });

        sut.ProcessThumbstick(GamepadBindingType.LeftThumbstick, new Vector2(1f, 0f), mappings);

        keyboard.Verify(k => k.KeyDown(Key.D), Times.Once);
        mouse.Verify(
            m => m.MoveBy(It.Is<int>(dx => dx != 0), It.IsAny<int>(), It.IsAny<float>()),
            Times.Once);
        mouse.Verify(m => m.MoveBy(0, 0, It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public void ProcessThumbstick_MouseLookOnRightStick_InvokesMoveBy()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.RightThumbstick, Value = "Y" },
                KeyboardKey = "mousey",
                Trigger = TriggerMoment.Pressed
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { });

        sut.ProcessThumbstick(GamepadBindingType.RightThumbstick, new Vector2(0f, 1f), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>()),
            Times.AtLeastOnce);
    }
}
