using System.Collections.Generic;
using System.Numerics;
using System.Windows.Input;
using GamepadMapperGUI.Core;
using GamepadMapperGUI.Interfaces.Services.Input;
using GamepadMapperGUI.Models;
using Moq;
using GamepadButtons = GamepadMapperGUI.Models.GamepadButtons;

namespace GamepadMapping.Tests.Core.Processing;

public sealed class AnalogMappingProcessorTests
{
    private static PlayStationInputState PsState(in PlayStationTouchPoint primary) =>
        new(
            Buttons: GamepadButtons.None,
            LeftThumbstick: Vector2.Zero,
            RightThumbstick: Vector2.Zero,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            Gyroscope: Vector3.Zero,
            IsTouchpadPressed: false,
            PrimaryTouch: primary,
            SecondaryTouch: new PlayStationTouchPoint(false, 0, 0f, 0f),
            TimestampMs: 0);

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
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()),
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
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()),
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
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()),
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
            m => m.MoveBy(It.Is<int>(dx => dx != 0), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()),
            Times.Once);
        mouse.Verify(m => m.MoveBy(0, 0, It.IsAny<float>(), It.IsAny<GamepadBindingType?>()), Times.Never);
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
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ProcessTouchpad_WhenPlayStationStateNull_DoesNotEmitTouchpadMouseMotion()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
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

        sut.ProcessTouchpad(null, mappings, isConsumed: false);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.Never);
    }

    [Fact]
    public void ProcessTouchpad_WhenTouchpadConsumed_DoesNotEmitTouchpadMouseMotion()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
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

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 1, 0.5f, 0.5f)), mappings, isConsumed: true);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.Never);
    }

    [Fact]
    public void ProcessTouchpad_MouseLook_FirstContactSample_DoesNotEmitDeltaMotion()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
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
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 7, 0.4f, 0.5f)), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.Never);
    }

    [Fact]
    public void ProcessTouchpad_MouseLook_SecondSampleWithSameTrackingId_EmitsMoveBy()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
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
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 3, 0.5f, 0.5f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 3, 0.62f, 0.5f)), mappings);

        mouse.Verify(
            m => m.MoveBy(It.Is<int>(dx => dx != 0), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ProcessTouchpad_MouseLook_NewTrackingId_DoesNotTreatAsContinuation_PreventsSpuriousJump()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
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
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 1, 0.95f, 0.5f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 2, 0.05f, 0.5f)), mappings);

        mouse.Verify(
            m => m.MoveBy(It.Is<int>(dx => dx != 0), It.IsAny<int>(), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.Never);
    }

    [Fact]
    public void ProcessTouchpad_SwipeUpOnLift_DispatchesSwipeMappingOnce()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "SWIPE_UP" },
                KeyboardKey = "Space",
                Trigger = TriggerMoment.Tap
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var analog = new AnalogProcessor();
        var dispatched = new List<MappingEntry>();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { },
            dispatchTouchpadDiscreteAction: dispatched.Add);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 1, 0.5f, 0.78f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 1, 0.5f, 0.12f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(false, 0, 0f, 0f)), mappings);

        Assert.Single(dispatched);
        Assert.Equal("SWIPE_UP", dispatched[0].From!.Value);
    }

    [Fact]
    public void ProcessTouchpad_MouseLookY_SecondSampleWithSameTrackingId_EmitsVerticalMoveBy()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEY" },
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
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 9, 0.5f, 0.5f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 9, 0.5f, 0.68f)), mappings);

        mouse.Verify(
            m => m.MoveBy(It.IsAny<int>(), It.Is<int>(dy => dy != 0), It.IsAny<float>(), GamepadBindingType.Touchpad),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ProcessTouchpad_AfterForceRelease_FirstActiveSample_DoesNotUsePriorDeltaBaseline()
    {
        var mappings = new List<MappingEntry>
        {
            new()
            {
                From = new GamepadBinding { Type = GamepadBindingType.Touchpad, Value = "MOUSEX" },
                KeyboardKey = "mousex",
                Trigger = TriggerMoment.Pressed
            }
        };

        var keyboard = new Mock<IKeyboardEmulator>();
        var mouse = new Mock<IMouseEmulator>();
        var nonZeroPixelMoves = 0;
        mouse.Setup(m => m.MoveBy(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<float>(), It.IsAny<GamepadBindingType?>()))
            .Callback<int, int, float, GamepadBindingType?>((dx, dy, _, _) =>
            {
                if (dx != 0 || dy != 0)
                    nonZeroPixelMoves++;
            });

        var analog = new AnalogProcessor();
        var sut = new AnalogMappingProcessor(
            analog,
            keyboard.Object,
            mouse.Object,
            () => true,
            _ => { },
            _ => { },
            getAnalogChangeEpsilon: () => 0.001f);

        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 4, 0.5f, 0.5f)), mappings);
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 4, 0.75f, 0.5f)), mappings);
        Assert.True(nonZeroPixelMoves >= 1);

        sut.ForceReleaseAnalogOutputs();
        var afterForce = nonZeroPixelMoves;
        sut.ProcessTouchpad(PsState(new PlayStationTouchPoint(true, 4, 0.76f, 0.5f)), mappings);

        Assert.Equal(afterForce, nonZeroPixelMoves);
    }
}
